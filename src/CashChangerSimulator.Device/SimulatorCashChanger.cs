using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.Device.Facades;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using R3;

namespace CashChangerSimulator.Device;

/// <summary>UPOS の CashChanger サービスオブジェクトのシミュレータークラス。</summary>
/// <remarks>仮想的な現金処理デバイスの振る舞いをシミュレートします。</remarks>
[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Virtual Cash Changer Simulator", 1, 14)]
public class SimulatorCashChanger : CashChangerBasic, ICashChangerStatusSink, IUposEventSink, IDeviceStateProvider
{
    internal readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly CashChangerManager _manager;
    private readonly OverallStatusAggregator _statusAggregator;
    private SimulatorConfiguration _config;

    internal readonly DepositController _depositController;
    private readonly DispenseController _dispenseController;
    internal readonly HardwareStatusManager _hardwareStatusManager;
    private readonly ILogger<SimulatorCashChanger> _logger;
    private readonly StatusCoordinator _statusCoordinator;
    private readonly UposDispenseFacade _dispenseFacade;
    private readonly DirectIOHandler _directIOHandler = new();
    private readonly DiagnosticController _diagnosticController;
    private readonly IUposConfigurationManager _configManager;
    private readonly IUposEventNotifier _eventNotifier;

    private readonly DepositFacade _depositFacade;
    private readonly InventoryFacade _inventoryFacade;
    private readonly DiagnosticsFacade _diagnosticsFacade;

    private string _checkHealthText = "OK";

    /// <summary>VerifyState チェックをスキップするかどうかを取得または設定します。</summary>
    public virtual bool SkipStateVerification
    {
        get => _mediator.SkipStateVerification;
        set
        {
            if (_mediator.SkipStateVerification == value) return;
            _mediator.SkipStateVerification = value;
            UpdateLifecycleHandler();
        }
    }

    private readonly LifecycleManager _lifecycleManager;
    private readonly UposMediator _mediator;

    private void UpdateLifecycleHandler()
    {
        // プロキシ生成時や初期化の非常に早い段階で呼ばれた場合に例外を防ぐためにnullチェックのみ行う
        if (_hardwareStatusManager == null || _mediator == null || _logger == null) return;
        _lifecycleManager.UpdateHandler(_mediator.SkipStateVerification);
    }

    /// <summary>デバイスの有効状態を取得または設定します。</summary>
    public override bool DeviceEnabled
    {
        get => _lifecycleManager.DeviceEnabled;
        set => _lifecycleManager.DeviceEnabled = value;
    }

    /// <summary>データイベントの通知が有効かどうかを取得または設定します。</summary>
    public override bool DataEventEnabled
    {
        get => _lifecycleManager.DataEventEnabled;
        set => _lifecycleManager.DataEventEnabled = value;
    }

    /// <summary>入金データのリアルタイム通知が有効かどうかを取得または設定します。</summary>
    public override bool RealTimeDataEnabled
    {
        get => _depositFacade.RealTimeDataEnabled;
        set => _depositFacade.RealTimeDataEnabled = value;
    }

    /// <summary>デバイスがリアルタイムデータの通知能力を持っているかどうか。</summary>
    public override bool CapRealTimeData => _config.Simulation.CapRealTimeData;

    /// <summary>最新の操作の結果コードを取得または設定します。</summary>
    public int ResultCode
    {
        get => _mediator.ResultCode;
        set => _mediator.SetFailure((ErrorCode)value); // Simplified for setter
    }

    /// <summary>最新の操作の拡張結果コードを取得または設定します。</summary>
    public int ResultCodeExtended
    {
        get => _mediator.ResultCodeExtended;
        set => _mediator.SetFailure((ErrorCode)ResultCode, value);
    }

    /// <summary>現在のデバイスの状態を取得します。</summary>
    public override ControlState State => _lifecycleManager?.State ?? ControlState.Closed;

    void IUposEventSink.NotifyEvent(EventArgs e) => NotifyEvent(e);

    void IUposEventSink.QueueEvent(EventArgs e)
    {
        if (e is DataEventArgs de) QueueEvent(de);
        else if (e is StatusUpdateEventArgs se) QueueEvent(se);
    }

    /// <summary>イベントを通知し、必要に応じてキューに追加します。</summary>
    /// <param name="e">通知するイベント引数。</param>
    protected virtual void NotifyEvent(System.EventArgs e) =>
        _eventNotifier.NotifyEvent(e);

    /// <summary>外部からイベントを強制的に発生させます。</summary>
    public void FireEvent(EventArgs e) =>
        NotifyEvent(e);

    /// <summary>非同期処理中フラグを設定します。</summary>
    public void SetAsyncProcessing(bool isBusy) =>
        _mediator.IsBusy = isBusy;

    // ========== Simulator Lifecycle Delegation ==========

    /// <summary>デバイスをオープンします。</summary>
    public override void Open()
    {
        if (_lifecycleManager == null) UpdateLifecycleHandler();
        if (_lifecycleManager == null) throw new InvalidOperationException("Critical error: _lifecycleManager is null in Open()");

        _lifecycleManager.Open(base.Open);
    }

    /// <summary>デバイスをクローズします。</summary>
    public override void Close() => _lifecycleManager.Close(base.Close);

    /// <summary>デバイスを占有します。</summary>
    public override void Claim(int timeout) => _lifecycleManager.Claim(timeout, base.Claim);

    /// <summary>デバイスを解放します。</summary>
    public override void Release() => _lifecycleManager.Release(base.Release);

    /// <summary>デバイスが現在占有されているかどうかを取得します。</summary>
    public override bool Claimed => _lifecycleManager?.Claimed ?? false;

    /// <summary>SimulatorCashChanger の新しいインスタンスを初期化します。</summary>
    public SimulatorCashChanger(SimulatorDependencies deps)
    {
        _logger = LogProvider.CreateLogger<SimulatorCashChanger>();
        _logger.LogInformation("SimulatorCashChanger initializing...");

        _hardwareStatusManager = deps.HardwareStatusManager ?? new HardwareStatusManager();
        
        // Mediator と LifecycleManager は他のコンポーネントが依存する可能性があるため早期に初期化
        _mediator = deps.Mediator as UposMediator ?? new UposMediator(this);
        _lifecycleManager = new LifecycleManager(_hardwareStatusManager, _mediator, _logger);
        UpdateLifecycleHandler();

        var actualConfigProvider = deps.ConfigProvider ?? new ConfigurationProvider();
        _config = actualConfigProvider.Config;

        DevicePath = "SimulatorCashChanger";

        _inventory = deps.Inventory ?? new Inventory();
        _history = deps.History ?? new TransactionHistory();
        _manager = deps.Manager ?? new CashChangerManager(_inventory, _history, new ChangeCalculator());

        _depositController = deps.DepositController ?? new DepositController(_inventory, _hardwareStatusManager, _manager, actualConfigProvider);
        _dispenseController = deps.DispenseController ?? new DispenseController(_manager, _hardwareStatusManager, new HardwareSimulator(actualConfigProvider));
        _diagnosticController = deps.DiagnosticController ?? new DiagnosticController(_inventory, _hardwareStatusManager);

        // Configuration and Event Notifier
        _configManager = deps.ConfigurationManager ?? new UposConfigurationManager(actualConfigProvider, _inventory, this);
        _configManager.Initialize();
        _eventNotifier = deps.EventNotifier ?? new UposEventNotifier(this);

        // Status monitors / Aggregator
        var monitors = _inventory
            .AllCounts
            .Select(kv => (kv.Key, Settings: actualConfigProvider.Config.GetDenominationSetting(kv.Key)))
            .Select(x =>
                new CashStatusMonitor(
                    _inventory,
                    x.Key,
                    x.Settings.NearEmpty,
                    x.Settings.NearFull,
                    x.Settings.Full))
            .ToList();

        _statusAggregator =
            deps.AggregatorProvider
            ?.Aggregator
            ?? new OverallStatusAggregator(monitors);

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
            _dispenseController, _depositController,
            _hardwareStatusManager, _inventory,
            _mediator, _logger);
        // Deposit Facade initialization
        _depositFacade = new DepositFacade(_depositController, _mediator, _diagnosticController);
        // Inventory Facade initialization
        _inventoryFacade = new InventoryFacade(_inventory, _manager, _mediator);
        _diagnosticsFacade = new DiagnosticsFacade(_diagnosticController, _mediator);
        _logger.LogInformation("SimulatorCashChanger initialized.");
    }


    /// <summary>デバイスの健康状態を確認します。</summary>
    public override string CheckHealth(HealthCheckLevel level)
    {
        var report = _diagnosticsFacade.CheckHealth(level);
        _checkHealthText = report;
        return report;
    }

    /// <summary>健康状態のテキスト表現を取得します。</summary>
    public override string CheckHealthText => _checkHealthText;

    /// <summary>現金投入処理を開始します。</summary>
    public override void BeginDeposit()
        => _depositFacade.BeginDeposit();

    /// <summary>現金投入処理を終了します。</summary>
    public override void EndDeposit(CashDepositAction action)
        => _depositFacade.EndDeposit(action);

    /// <summary>投入された現金の計数を確定します。</summary>
    public override void FixDeposit()
    {
        _depositFacade.FixDeposit();
        if (DataEventEnabled && CapDepositDataEvent)
        {
            NotifyEvent(new DataEventArgs(0));
        }
    }

    /// <summary>現金投入処理を一時停止または再開します。</summary>
    public override void PauseDeposit(CashDepositPause control)
        => _depositFacade.PauseDeposit(control);

    /// <summary>入金セッション中に投入された現金を返却します。</summary>
    public virtual void RepayDeposit()
        => _depositFacade.RepayDeposit();

    /// <summary>指定された金額の釣銭を払い出します。</summary>
    public override void DispenseChange(int amount)
        => _dispenseFacade.DispenseByAmount(amount, _configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode), AsyncMode, (code, codeEx, wasAsync) => HandleDispenseResult(code, codeEx, wasAsync));

    /// <summary>指定された金種と枚数の現金を払い出します。</summary>
    public override void DispenseCash(CashCount[] cashCounts)
        => _dispenseFacade.DispenseByCashCounts(cashCounts, _configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode), AsyncMode, (code, codeEx, wasAsync) => HandleDispenseResult(code, codeEx, wasAsync));

    /// <summary>保留中の出金操作をすべてキャンセルします。</summary>
    public virtual void ClearOutput()
        => _dispenseFacade.ClearOutput();

    private void HandleDispenseResult(ErrorCode code, int codeEx, bool wasAsync) =>
        _mediator.HandleDispenseResult(code, codeEx, wasAsync);

    /// <summary>現在の現金在庫数を手動で調整します。</summary>
    /// <remarks>指定された金種の枚数で現在の在庫を上書きします。</remarks>
    public override void AdjustCashCounts(IEnumerable<CashCount> cashCounts)
        => _inventoryFacade.AdjustCashCounts(cashCounts, _configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode), _hardwareStatusManager);
 
    /// <summary>現在の現金在庫数を読み取ります。</summary>
    public override CashCounts ReadCashCounts()
        => _inventoryFacade.ReadCashCounts(_configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode));
 
    /// <summary>リサイクル在庫をすべて回収庫へ移動します。</summary>
    public virtual void PurgeCash()
        => _inventoryFacade.PurgeCash();

    /// <summary>ベンダー固有のコマンドをデバイスに送信します。</summary>
    public override DirectIOData DirectIO(int command, int data, object obj)
    {
        _mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
        var result = _directIOHandler.Handle(command, data, obj, this);
        _mediator.SetSuccess();
        return result;
    }

    /// <summary>統計情報を取得します。</summary>
    public override string RetrieveStatistics(string[] statistics)
        => _diagnosticsFacade.RetrieveStatistics(statistics);
  
    /// <summary>統計情報を更新します。</summary>
    public override void UpdateStatistics(Statistic[] statistics)
        => _diagnosticsFacade.UpdateStatistics(statistics);
  
    /// <summary>統計情報をリセットします。</summary>
    public override void ResetStatistics(string[] statistics)
        => _diagnosticsFacade.ResetStatistics(statistics);

    // ========== UPOS Property Overrides (Merged from Properties.cs) ==========

    /// <summary>デバイスの現在の状態（正常、空、ニアエンプティなど）を取得します。</summary>
    public override CashChangerStatus DeviceStatus =>
        _statusCoordinator?.LastCashChangerStatus ?? CashChangerStatus.OK;
    /// <summary>デバイスの現在の満杯状態（正常、満杯、ニアフルなど）を取得します。</summary>
    public override CashChangerFullStatus FullStatus =>
        _statusCoordinator?.LastFullStatus ?? CashChangerFullStatus.OK;

    /// <summary>非同期モードで動作するかどうかを取得または設定します。</summary>
    public override bool AsyncMode { get; set; }
    /// <summary>最後の非同期操作の結果コードを取得します。</summary>
    public override int AsyncResultCode => _mediator.AsyncResultCode;
    /// <summary>最後の非同期操作の拡張結果コードを取得します。</summary>
    public override int AsyncResultCodeExtended => _mediator.AsyncResultCodeExtended;

    /// <summary>現在アクティブな通貨コードを取得または設定します。</summary>
    public override string CurrencyCode
    {
        get => _configManager.CurrencyCode;
        set => _configManager.CurrencyCode = value;
    }
    /// <summary>サポートされている通貨コードの一覧を取得します。</summary>
    public override string[] CurrencyCodeList => _configManager.CurrencyCodeList;
    /// <summary>入金可能な通貨コードの一覧を取得します。</summary>
    public override string[] DepositCodeList => _configManager.DepositCodeList;

    /// <summary>アクティブな通貨の現在の現金一覧を取得します。</summary>
    public override CashUnits CurrencyCashList => _inventoryFacade.GetCashList(_configManager.CurrencyCode);
    /// <summary>入金可能な現金の一覧を取得します。</summary>
    public override CashUnits DepositCashList => CapDeposit ? _inventoryFacade.GetCashList(_configManager.CurrencyCode) : new CashUnits();
    /// <summary>払出可能な現金の一覧を取得します。</summary>
    public override CashUnits ExitCashList => _inventoryFacade.GetCashList(_configManager.CurrencyCode);

    /// <summary>現在投入されている現金の合計金額を取得します。</summary>
    public override int DepositAmount => _depositFacade.GetUposDepositAmount(UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode));
    /// <summary>現在投入されている現金の金種別枚数を取得します。</summary>
    public override CashCount[] DepositCounts => _depositFacade.GetUposDepositCounts(_configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode));
    /// <summary>現在の入金処理の状態を取得します。</summary>
    public override CashDepositStatus DepositStatus => _depositFacade.DepositStatus;

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
    /// <summary>一括回収（Purge）をサポートしているかどうかを取得します。</summary>
    public virtual bool CapPurgeCash => true;

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

    /// <summary>統計情報の報告をサポートしているかどうかを取得します。</summary>
    public override bool CapStatisticsReporting => true;
    /// <summary>統計情報の更新をサポートしているかどうかを取得します。</summary>
    public override bool CapUpdateStatistics => true;

    /// <summary>デバイス名を取得します。</summary>
    public override string DeviceName => "SimulatorCashChanger";
    /// <summary>デバイスの説明を取得します。</summary>
    public override string DeviceDescription => "Virtual Cash Changer Simulator";
}
