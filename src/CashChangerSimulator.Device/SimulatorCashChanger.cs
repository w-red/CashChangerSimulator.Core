using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using MicroResolver;
using R3;
using CashChangerSimulator.Device.Strategies;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Lifecycle;
using ZLogger;

namespace CashChangerSimulator.Device;

/// <summary>UPOS の CashChanger サービスオブジェクトのシミュレータークラス。</summary>
/// <remarks>仮想的な現金処理デバイスの振る舞いをシミュレートします。</remarks>
[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Virtual Cash Changer Simulator", 1, 14)]
public class SimulatorCashChanger : CashChangerBasic, ICashChangerStatusSink
{
    internal readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly CashChangerManager _manager;
    private readonly OverallStatusAggregator _statusAggregator;
    private readonly SimulatorConfiguration _config;

    internal readonly DepositController _depositController;
    private readonly DispenseController _dispenseController;
    internal readonly HardwareStatusManager _hardwareStatusManager;
    private readonly ILogger<SimulatorCashChanger> _logger;
    private readonly StatusCoordinator _statusCoordinator;
    private readonly UposDispenseFacade _dispenseFacade;
    private readonly DirectIOHandler _directIOHandler = new();

    // Async processing state
    private bool _asyncProcessing;
    private int _asyncResultCode;
    private int _asyncResultCodeExtended;
    private bool _deviceEnabled;
    private bool _dataEventEnabled;
    private bool _isClaimed;

    /// <summary>VerifyState チェックをスキップするかどうかを取得または設定します。</summary>
    public virtual bool SkipStateVerification { get; set; }
    private readonly UposOperationHelper _operationHelper;

    /// <summary>デバイスの有効状態を取得または設定します。</summary>
    public override bool DeviceEnabled
    {
        get => SkipStateVerification ? _deviceEnabled : base.DeviceEnabled;
        set
        {
            if (value)
            {
                if (State == ControlState.Closed)
                {
                    throw new PosControlException("Device is not open.", ErrorCode.Closed);
                }

                if (!Claimed)
                {
                    throw new PosControlException("Device must be claimed before enabling.", ErrorCode.Illegal);
                }
            }

            if (SkipStateVerification)
            {
                _deviceEnabled = value;
                _logger.ZLogInformation($"DeviceEnabled set to {value}.");
                return;
            }

            if (value == base.DeviceEnabled) return;

            base.DeviceEnabled = value;
            _logger.ZLogInformation($"DeviceEnabled set to {value}.");
        }
    }

    /// <summary>データイベントの通知が有効かどうかを取得または設定します。</summary>
    public override bool DataEventEnabled
    {
        get => SkipStateVerification ? _dataEventEnabled : base.DataEventEnabled;
        set
        {
            if (SkipStateVerification)
            {
                _dataEventEnabled = value;
                return;
            }
            base.DataEventEnabled = value;
        }
    }

    /// <summary>入金データのリアルタイム通知が有効かどうかを取得または設定します。</summary>
    public override bool RealTimeDataEnabled
    {
        get => _depositController.RealTimeDataEnabled;
        set => _depositController.RealTimeDataEnabled = value;
    }

    /// <summary>デバイスがリアルタイムデータの通知能力を持っているかどうか。</summary>
    public override bool CapRealTimeData => _config.Simulation.CapRealTimeData;

    /// <summary>最新の操作の結果コードを取得または設定します。</summary>
    public int ResultCode { get; set; }

    /// <summary>最新の操作の拡張結果コードを取得または設定します。</summary>
    public int ResultCodeExtended { get; set; }

    /// <summary>現在のデバイスの状態を取得します。</summary>
    public override ControlState State
    {
        get
        {
            if (!_hardwareStatusManager.IsConnected.Value) return ControlState.Closed;
            return _asyncProcessing ? ControlState.Busy : ControlState.Idle;
        }
    }

    /// <summary>イベントを通知し、必要に応じてキューに追加します。</summary>
    /// <param name="e">通知するイベント引数。</param>
    protected virtual void NotifyEvent(EventArgs e)
    {
        if (SkipStateVerification) return;

        if (e is DataEventArgs de)
        {
            QueueEvent(de);
        }
        else if (e is StatusUpdateEventArgs se)
        {
            QueueEvent(se);
        }
    }

    /// <summary>外部クラス（Strategy/Coordinator等）からイベントを発生させます。</summary>
    public void FireEvent(EventArgs e) =>
        NotifyEvent(e);

    /// <summary>非同期処理中フラグを設定します。</summary>
    public void SetAsyncProcessing(bool isBusy) =>
        _asyncProcessing = isBusy;

    // ========== Simulator UI Helpers (Overridden in InternalSimulatorCashChanger) ==========

    /// <summary>デバイスをオープンします。外部（シミュレータ）からは継承クラスのメソッドを使用してください。</summary>
    /// <summary>デバイスをオープンし、通信を開始します。</summary>
    public override void Open()
    {
        if (SkipStateVerification)
        {
            _hardwareStatusManager.SetConnected(true);
            _logger.ZLogInformation($"Device opened (Verification Skipped).");
            return;
        }
        base.Open();
        _hardwareStatusManager.SetConnected(true);
    }

    /// <summary>デバイスをクローズします。</summary>
    public override void Close()
    {
        if (SkipStateVerification)
        {
            _hardwareStatusManager.SetConnected(false);
            _logger.ZLogInformation($"Device closed (Verification Skipped).");
            return;
        }
        base.Close();
        _hardwareStatusManager.SetConnected(false);
    }

    /// <summary>デバイスを占有します。</summary>
    public override void Claim(int timeout)
    {
        if (SkipStateVerification)
        {
            if (State == ControlState.Closed)
            {
                throw new PosControlException("Device is not open.", ErrorCode.Closed);
            }
            _isClaimed = true;
            _logger.ZLogInformation($"Device claimed (Verification Skipped).");
            return;
        }
        base.Claim(timeout);
    }

    /// <summary>デバイスを解放します。</summary>
    public override void Release()
    {
        if (SkipStateVerification)
        {
            _isClaimed = false;
            _logger.ZLogInformation($"Device released (Verification Skipped).");
            return;
        }
        base.Release();
    }

    /// <summary>デバイスが現在占有されているかどうかを取得します。</summary>
    public override bool Claimed => SkipStateVerification ? _isClaimed : base.Claimed;

    /// <summary>SimulatorCashChanger の新しいインスタンスを初期化します。DI時は [Inject] 属性のものが優先されます。</summary>
    [Inject]
    public SimulatorCashChanger(
        ConfigurationProvider? configProvider = null,
        Inventory? inventory = null,
        TransactionHistory? history = null,
        CashChangerManager? manager = null,
        DepositController? depositController = null,
        DispenseController? dispenseController = null,
        OverallStatusAggregatorProvider? aggregatorProvider = null,
        HardwareStatusManager? hardwareStatusManager = null)
    {
        var actualConfigProvider = configProvider ?? new ConfigurationProvider();
        _config = actualConfigProvider.Config;

        DevicePath = "SimulatorCashChanger";

        _hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();

        _logger = LogProvider.CreateLogger<SimulatorCashChanger>();
        _logger.ZLogInformation($"SimulatorCashChanger initialized.");

        _inventory = inventory ?? new Inventory();
        _history = history ?? new TransactionHistory();
        _manager = manager ?? new CashChangerManager(_inventory, _history, new ChangeCalculator());

        _depositController = depositController ?? new DepositController(_inventory, _hardwareStatusManager, _manager, actualConfigProvider);
        _dispenseController = dispenseController ?? new DispenseController(_manager, _hardwareStatusManager, new HardwareSimulator(actualConfigProvider));

        // Status monitors / Aggregator
        var monitors = _inventory
            .AllCounts
            .Select(kv => (kv.Key, _config.GetDenominationSetting(kv.Key)))
            .Select(x =>
                new CashStatusMonitor(
                    _inventory,
                    x.Key,
                    x.Item2.NearEmpty,
                    x.Item2.NearFull,
                    x.Item2.Full))
            .ToList();

        _statusAggregator =
            aggregatorProvider
            ?.Aggregator
            ?? new OverallStatusAggregator(monitors);

        // Active currency initialization
        _activeCurrencyCode =
            _config
            .Inventory
            .Keys
            .FirstOrDefault()
            ?? "JPY";

        // Subscribe to status changes via StatusCoordinator (Observer Pattern)
        _statusCoordinator = new StatusCoordinator(
            this,
            _statusAggregator,
            _hardwareStatusManager,
            _depositController,
            _dispenseController);
        _statusCoordinator.Start();

        // Dispense Facade initialization
        _dispenseFacade = new UposDispenseFacade(
            _dispenseController,
            _depositController,
            _hardwareStatusManager,
            _inventory,
            _logger);

        _operationHelper = new UposOperationHelper(this);
    }

    private string _activeCurrencyCode;

    /// <summary>デバイスの健康状態を確認します。</summary>
    public override string CheckHealth(HealthCheckLevel level) => "OK";
    /// <summary>健康状態のテキスト表現を取得します。</summary>
    public override string CheckHealthText => "OK";

    // ========== Deposit Methods (UPOS v1.5+) ==========

    /// <summary>現金投入処理を開始します。</summary>
    public override void BeginDeposit()
    {
        _operationHelper.VerifyState(SkipStateVerification);
        UposOperationHelper.ThrowIfBusy(_asyncProcessing);
        _depositController.BeginDeposit();
    }

    /// <summary>現金投入処理を終了します。</summary>
    public override void EndDeposit(CashDepositAction action)
    {
        _operationHelper.VerifyState(SkipStateVerification);
        _depositController.EndDeposit(action);
    }

    /// <summary>投入された現金の計数を確定します。</summary>
    public override void FixDeposit()
    {
        _operationHelper.VerifyState(SkipStateVerification);
        _depositController.FixDeposit();

        if (DataEventEnabled && CapDepositDataEvent)
        {
            NotifyEvent(new DataEventArgs(0));
        }
    }

    /// <summary>現金投入処理を一時停止または再開します。</summary>
    public override void PauseDeposit(CashDepositPause control)
    {
        _operationHelper.VerifyState(SkipStateVerification);
        _depositController.PauseDeposit(control);
    }

    // ========== Dispense Methods ==========

    /// <summary>指定された金額の釣銭を払い出します。</summary>
    public override void DispenseChange(int amount)
    {
        _operationHelper.VerifyState(SkipStateVerification);
        UposOperationHelper.ThrowIfBusy(_asyncProcessing);
        UposOperationHelper.ThrowIfDepositInProgress(_depositController.IsDepositInProgress);
        _dispenseFacade!.DispenseByAmount(amount, CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_activeCurrencyCode), AsyncMode, HandleDispenseResult);
    }

    /// <summary>指定された金種と枚数の現金を払い出します。</summary>
    public override void DispenseCash(CashCount[] cashCounts)
    {
        _operationHelper.VerifyState(SkipStateVerification);
        UposOperationHelper.ThrowIfBusy(_asyncProcessing);
        UposOperationHelper.ThrowIfDepositInProgress(_depositController.IsDepositInProgress);
        _dispenseFacade!.DispenseByCashCounts(cashCounts, _activeCurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_activeCurrencyCode), AsyncMode, HandleDispenseResult);
    }

    private void HandleDispenseResult(ErrorCode code, int codeEx, bool wasAsync)
    {
        ResultCode = (int)code;
        ResultCodeExtended = codeEx;
        if (wasAsync)
        {
            _asyncResultCode = (int)code;
            _asyncResultCodeExtended = codeEx;
            _asyncProcessing = false;
            NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.AsyncFinished));
        }
    }

    // ========== AdjustCashCounts ==========

    /// <summary>現在の現金在庫数を手動で調整します。</summary>
    /// <remarks>指定された金種の枚数で現在の在庫を上書きします。</remarks>
    public override void AdjustCashCounts(IEnumerable<CashCount> cashCounts)
    {
        _operationHelper.VerifyState(SkipStateVerification);
        UposOperationHelper.ThrowIfBusy(_asyncProcessing);
        UposOperationHelper.ThrowIfDepositInProgress(_depositController.IsDepositInProgress);
        if (_hardwareStatusManager.IsJammed.Value)
        {
            throw new PosControlException("Device is jammed. Cannot adjust cash counts.", ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.Jam);
        }

        var dict = CashCountAdapter.ToDenominationDict(cashCounts, _activeCurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_activeCurrencyCode));
        foreach (var (key, count) in dict)
        {
            _inventory.SetCount(key, count);
        }
        _logger.ZLogInformation($"AdjustCashCounts completed. Updated {dict.Count} denominations.");
    }

    // ========== ReadCashCounts ==========

    /// <summary>現在の現金在庫数を読み取ります。</summary>
    public override CashCounts ReadCashCounts()
    {
        _operationHelper.VerifyState(SkipStateVerification);
        UposOperationHelper.ThrowIfBusy(_asyncProcessing);
        var sorted = _inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .OrderBy(kv => kv.Key.Type)
            .ThenBy(kv => kv.Key.Value);

        var list = sorted.Select(kv => CashCountAdapter.ToCashCount(kv.Key, kv.Value, UposCurrencyHelper.GetCurrencyFactor(_activeCurrencyCode))).ToList();

        return new CashCounts([.. list], _inventory.HasDiscrepancy);
    }

    /// <summary>ベンダー固有のコマンドをデバイスに送信します。</summary>
    public override DirectIOData DirectIO(int command, int data, object obj)
    {
        _operationHelper.VerifyState(SkipStateVerification);
        return _directIOHandler.Handle(command, data, obj, this);
    }

    // ========== UPOS Property Overrides (Merged from Properties.cs) ==========

    /// <summary>デバイスの現在の状態（正常、空、ニアエンプティなど）を取得します。</summary>
    public override CashChangerStatus DeviceStatus => _statusCoordinator?.LastCashChangerStatus ?? CashChangerStatus.OK;
    /// <summary>デバイスの現在の満杯状態（正常、満杯、ニアフルなど）を取得します。</summary>
    public override CashChangerFullStatus FullStatus => _statusCoordinator?.LastFullStatus ?? CashChangerFullStatus.OK;

    /// <summary>非同期モードで動作するかどうかを取得または設定します。</summary>
    public override bool AsyncMode { get; set; }
    /// <summary>最後の非同期操作の結果コードを取得します。</summary>
    public override int AsyncResultCode => _asyncResultCode;
    /// <summary>最後の非同期操作の拡張結果コードを取得します。</summary>
    public override int AsyncResultCodeExtended => _asyncResultCodeExtended;

    /// <summary>現在アクティブな通貨コードを取得または設定します。</summary>
    public override string CurrencyCode
    {
        get => _activeCurrencyCode;
        set
        {
            _activeCurrencyCode =
                CurrencyCodeList.Contains(value)
                ? value
                : throw new PosControlException($"Unsupported currency: {value}", ErrorCode.Illegal);
        }
    }
    /// <summary>サポートされている通貨コードの一覧を取得します。</summary>
    public override string[] CurrencyCodeList => [.. _config.Inventory.Keys.OrderBy(c => c)];
    /// <summary>入金可能な通貨コードの一覧を取得します。</summary>
    public override string[] DepositCodeList => CurrencyCodeList;

    /// <summary>アクティブな通貨の現在の現金一覧を取得します。</summary>
    public override CashUnits CurrencyCashList => UposCurrencyHelper.BuildCashUnits(_inventory, _activeCurrencyCode);
    /// <summary>入金可能な現金の一覧を取得します。</summary>
    public override CashUnits DepositCashList => CapDeposit ? UposCurrencyHelper.BuildCashUnits(_inventory, _activeCurrencyCode) : new CashUnits();
    /// <summary>払出可能な現金の一覧を取得します。</summary>
    public override CashUnits ExitCashList => UposCurrencyHelper.BuildCashUnits(_inventory, _activeCurrencyCode);

    /// <summary>現在投入されている現金の合計金額を取得します。</summary>
    public override int DepositAmount => (int)Math.Round(_depositController.DepositAmount * UposCurrencyHelper.GetCurrencyFactor(_activeCurrencyCode));
    /// <summary>現在投入されている現金の金種別枚数を取得します。</summary>
    public override CashCount[] DepositCounts
    {
        get => [.. _depositController.DepositCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .Select(kv => CashCountAdapter.ToCashCount(kv.Key, kv.Value, UposCurrencyHelper.GetCurrencyFactor(_activeCurrencyCode)))];
    }
    /// <summary>現在の入金処理の状態を取得します。</summary>
    public override CashDepositStatus DepositStatus => _depositController.DepositStatus;

    /// <summary>現在の排出口インデックスを取得または設定します。</summary>
    public override int CurrentExit { get => 1; set { } }
    /// <summary>デバイスの排出口の総数を取得します。</summary>
    public override int DeviceExits => 1;

    /// <summary>入金機能があるかどうかを取得します。</summary>
    public override bool CapDeposit => true;
    /// <summary>入金データイベントをサポートしているかどうかを取得します。</summary>
    public override bool CapDepositDataEvent => true;
    /// <summary>入金の一時停止をサポートしているかどうかを取得します。</summary>
    public override bool CapPauseDeposit => true;
    /// <summary>入金の返却（払い戻し）をサポートしているかどうかを取得します。</summary>
    public override bool CapRepayDeposit => true;

    /// <summary>不一致の検出をサポートしているかどうかを取得します。</summary>
    /// <remarks>回収庫やリジェクト庫が存在する場合の在庫不一致検出能力を示します。</remarks>
    public override bool CapDiscrepancy => true;
    /// <summary>満杯センサーをサポートしているかどうかを取得します。</summary>
    public override bool CapFullSensor => true;
    /// <summary>ニアフルセンサーをサポートしているかどうかを取得します。</summary>
    public override bool CapNearFullSensor => true;
    /// <summary>ニアエンプティセンサーをサポートしているかどうかを取得します。</summary>
    public override bool CapNearEmptySensor => true;
    /// <summary>空センサーをサポートしているかどうかを取得します。</summary>
    public override bool CapEmptySensor => true;

    /// <summary>デバイス名を取得します。</summary>
    public override string DeviceName => "SimulatorCashChanger";
    /// <summary>デバイスの説明を取得します。</summary>
    public override string DeviceDescription => "Virtual Cash Changer Simulator";
}
