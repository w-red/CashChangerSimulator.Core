using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using MoneyKind4Opos.Currencies.Interfaces;
using MicroResolver;
using R3;
using CashChangerSimulator.Device.Strategies;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Lifecycle;
using ZLogger;

namespace CashChangerSimulator.Device;

/// <summary>UPOS の CashChanger サービスオブジェクトをシミュレートするクラス。</summary>
/// <remarks>プロパティオーバーライドは <seealso cref="SimulatorCashChanger"/> の部分クラス SimulatorCashChanger.Properties.cs に定義されています。</remarks>
[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Virtual Cash Changer Simulator", 1, 14)]
public partial class SimulatorCashChanger : CashChangerBasic, ICashChangerStatusSink
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
    private StatusCoordinator? _statusCoordinator;
    private UposDispenseFacade? _dispenseFacade;

    // Status tracking for StatusUpdateEvent transitions

    // Async processing state
    private bool _asyncProcessing;
    private int _asyncResultCode;
    private int _asyncResultCodeExtended;

    private readonly Dictionary<int, IDirectIOCommand> _directIOCommands = new();

    /// <summary>テスト用のイベント通知アクション。</summary>
    internal Action<EventArgs>? OnEventQueued; // For testing

    /// <summary>テスト用: VerifyState チェックをスキップする。OPOS ライフサイクルが利用できない単体テスト環境で使用。</summary>
    public bool SkipStateVerification { get; set; }

    /// <summary>デバイスの有効状態を取得または設定します。シミュレータでは常に成功するようにオーバーライドします。</summary>
    private bool _deviceEnabled;
    /// <summary>デバイスの有効状態を取得または設定します。</summary>
    public override bool DeviceEnabled
    {
        get => _deviceEnabled;
        set
        {
            if (!SkipStateVerification)
            {
                if (value && _lifecycleState is ClosedState)
                {
                    throw new PosControlException("Device is not open.", ErrorCode.Closed);
                }
                if (value && _lifecycleState is not ClaimedState)
                {
                    throw new PosControlException("Device is not claimed.", ErrorCode.Illegal);
                }
            }
            _deviceEnabled = value;
            _logger.ZLogInformation($"DeviceEnabled set to {value}.");
        }
    }
    /// <summary>データイベント通知の有効状態を取得または設定します。シミュレータでは常に成功するようにオーバーライドします。</summary>
    public override bool DataEventEnabled { get; set; }

    /// <summary>入金データのリアルタイム通知が有効かどうかを取得または設定します。</summary>
    public override bool RealTimeDataEnabled { get; set; }

    /// <summary>デバイスがリアルタイムデータの通知能力を持っているかどうか。</summary>
    public override bool CapRealTimeData => true;

    /// <summary>最新の操作の結果コードを取得または設定します。</summary>
    public int ResultCode { get; set; }

    /// <summary>最新の操作の拡張結果コードを取得または設定します。</summary>
    public int ResultCodeExtended { get; set; }

    /// <summary>現在のデバイスの状態を取得します。</summary>
    public override ControlState State
    {
        get
        {
            if (_lifecycleState is ClosedState) return ControlState.Closed;
            if (_asyncProcessing) return ControlState.Busy;
            return ControlState.Idle;
        }
    }

    /// <summary>イベントを通知し、必要に応じてキューに追加します。</summary>
    /// <param name="e">通知するイベント引数。</param>
    protected virtual void NotifyEvent(EventArgs e)
    {
        OnEventQueued?.Invoke(e);
        if (e is DataEventArgs de)
        {
            QueueEvent(de);
            
            // Phase 24.1: Record DataEvent in TransactionHistory for UI Activity Feed
            _history.Add(new TransactionEntry(
                DateTime.Now,
                TransactionType.DataEvent,
                0,
                new Dictionary<DenominationKey, int>()
            ));
        }
        else if (e is StatusUpdateEventArgs se)
        {
            QueueEvent(se);
        }
    }

    /// <summary>外部クラス（Strategy/Coordinator等）からイベントを発生させます。</summary>
    public void FireEvent(EventArgs e)
    {
        NotifyEvent(e);
    }

    /// <summary>非同期処理中フラグを設定します。</summary>
    public void SetAsyncProcessing(bool isBusy)
    {
        _asyncProcessing = isBusy;
    }

    // ========== Simulator UI Helpers for Lifecycle Verification ==========

    private IDeviceState _lifecycleState = new ClosedState();
    private DeviceLifecycleContext? _lifecycleContext;

    /// <summary>シミュレータUIからの Open 呼び出しを受け付けます。</summary>
    public new virtual void Open()
    {
        _lifecycleContext ??= new DeviceLifecycleContext(_hardwareStatusManager, _logger, v => DeviceEnabled = v);
        _lifecycleState = _lifecycleState.Open(_lifecycleContext);
    }

    /// <summary>シミュレータUIからの Close 呼び出しを受け付けます。</summary>
    public new virtual void Close()
    {
        _lifecycleContext ??= new DeviceLifecycleContext(_hardwareStatusManager, _logger, v => DeviceEnabled = v);
        _lifecycleState = _lifecycleState.Close(_lifecycleContext);
    }

    /// <summary>シミュレータUIからの Claim 呼び出しを受け付けます。</summary>
    public new virtual void Claim(int timeout)
    {
        _lifecycleContext ??= new DeviceLifecycleContext(_hardwareStatusManager, _logger, v => DeviceEnabled = v);
        _lifecycleState = _lifecycleState.Claim(_lifecycleContext, timeout);
    }

    /// <summary>シミュレータUIからの Release 呼び出しを受け付けます。</summary>
    public new virtual void Release()
    {
        _lifecycleContext ??= new DeviceLifecycleContext(_hardwareStatusManager, _logger, v => DeviceEnabled = v);
        _lifecycleState = _lifecycleState.Release(_lifecycleContext);
    }

    /// <summary>デフォルト構成で SimulatorCashChanger の新しいインスタンスを初期化します（主にテスト用）。</summary>
    public SimulatorCashChanger()
        : this(null, null, null, null, null, null, null, null)
    {
    }

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
        
        _depositController = depositController ?? new DepositController(_inventory, _hardwareStatusManager);
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

        InitializeDirectIOCommands();
    }

    private void InitializeDirectIOCommands()
    {
        var strategies = new IDirectIOCommand[]
        {
            new SetOverlapStrategy(),
            new SetJamStrategy(),
            new SetDiscrepancyStrategy(),
            new SimulateRemovedStrategy(),
            new SimulateInsertedStrategy(),
            new GetVersionStrategy(),
            new AdjustCashCountsStrStrategy(),
            new GetDepositedSerialsStrategy()
        };

        foreach (var strategy in strategies)
        {
            _directIOCommands[strategy.CommandCode] = strategy;
        }
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
        VerifyState();
        ThrowIfBusy();
        _depositController.BeginDeposit();
    }

    /// <summary>現金投入処理を終了します。</summary>
    public override void EndDeposit(CashDepositAction action)
    {
        VerifyState();
        _depositController.EndDeposit(action);
    }

    /// <summary>投入された現金の計数を確定します。</summary>
    public override void FixDeposit()
    {
        VerifyState();
        _depositController.FixDeposit();

        if (DataEventEnabled && CapDepositDataEvent)
        {
            QueueEvent(new DataEventArgs(0));
        }
    }

    /// <summary>現金投入処理を一時停止または再開します。</summary>
    public override void PauseDeposit(CashDepositPause control)
    {
        VerifyState();
        _depositController.PauseDeposit(control);
    }

    // ========== Dispense Methods ==========

    private void ThrowIfDepositInProgress()
    {
        if (_depositController.IsDepositInProgress)
        {
            throw new PosControlException(
                "Cash cannot be dispensed because cash acceptance is in progress.",
                ErrorCode.Illegal);
        }
    }

    private void ThrowIfBusy()
    {
        if (_asyncProcessing)
        {
            throw new PosControlException("Device is busy with an asynchronous operation.", ErrorCode.Busy);
        }
    }

    /// <summary>UPOS ライフサイクルの状態を検証します。Open/Claim/Enable が行われていない場合は例外をスローします。</summary>
    private void VerifyState()
    {
        if (SkipStateVerification) return;

        if (State == ControlState.Closed)
        {
            throw new PosControlException("Device is not open.", ErrorCode.Closed);
        }
        if (State is ControlState.Idle or ControlState.Error)
        {
            // Idle/Error means Open but not Claimed or not Enabled
            // For simplicity, we allow operations if State is Idle or Error (means at least Open)
        }
    }

    /// <summary>指定された金額の釣銭を払い出します。</summary>
    public override void DispenseChange(int amount)
    {
        VerifyState();
        ThrowIfBusy();
        _dispenseFacade!.DispenseByAmount(amount, CurrencyCode, GetCurrencyFactor(), AsyncMode, HandleDispenseResult);
    }

    /// <summary>指定された金種と枚数の現金を払い出します。</summary>
    public override void DispenseCash(CashCount[] cashCounts)
    {
        VerifyState();
        ThrowIfBusy();
        _dispenseFacade!.DispenseByCashCounts(cashCounts, _activeCurrencyCode, GetCurrencyFactor(_activeCurrencyCode), AsyncMode, HandleDispenseResult);
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
    
    /// <summary>現在の現金在庫数を手動で調整（上書き）します。</summary>
    public override void AdjustCashCounts(IEnumerable<CashCount> cashCounts)
    {
        VerifyState();
        ThrowIfBusy();
        if (_hardwareStatusManager.IsJammed.Value)
        {
            throw new PosControlException("Device is jammed. Cannot adjust cash counts.", ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.Jam);
        }

        var dict = CashCountAdapter.ToDenominationDict(cashCounts, _activeCurrencyCode, GetCurrencyFactor(_activeCurrencyCode));
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
        VerifyState();
        ThrowIfBusy();
        var sorted = _inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .OrderBy(kv => kv.Key.Type)
            .ThenBy(kv => kv.Key.Value);

        var list = sorted.Select(kv => CashCountAdapter.ToCashCount(kv.Key, kv.Value, GetCurrencyFactor())).ToList();

        return new CashCounts([.. list], _inventory.HasDiscrepancy);
    }

    /// <summary>ベンダー固有のコマンドをデバイスに送信します。</summary>
    public override DirectIOData DirectIO(int command, int data, object obj)
    {
        VerifyState();
        if (_directIOCommands.TryGetValue(command, out var strategy))
        {
            return strategy.Execute(data, obj, this);
        }
        return new DirectIOData(data, obj);
    }

}
