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
using CashChangerSimulator.Device.Models;
using R3;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;

namespace CashChangerSimulator.Device;

/// <summary>Microsoft POS for .NET (OPOS) に準拠した仮想現金入出金機のシミュレータクラス。</summary>
/// <remarks>
/// Microsoft Point of Service SDK を通じて、標準的な OPOS インターフェースを提供します。
/// 内部的には複数の Facade と Controller を使用して、在庫管理、入金処理、出金処理、および診断機能を提供し、
/// 物理デバイスなしでのアプリケーション開発とテストを支援します。
/// </remarks>
[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Virtual Cash Changer Simulator", 1, 14)]
public class SimulatorCashChanger : CashChangerBasic, IUposEventSink, IDeviceStateProvider, ICashChangerStatusSink, IDisposable
{
    private readonly SimulatorContext _ctx;
    private readonly ILogger<SimulatorCashChanger> _logger;
    private readonly UposDispenseFacade _dispenseFacade;
    private readonly DepositFacade _depositFacade;
    private readonly InventoryFacade _inventoryFacade;
    private readonly DiagnosticsFacade _diagnosticsFacade;
    private readonly CapabilitiesFacade _capFacade;
    private readonly IUposConfigurationManager _configManager;
    private readonly IUposEventNotifier _eventNotifier;
    private readonly DirectIOHandler _directIOHandler = new();
    private string _checkHealthText = "OK";

    internal Inventory _inventory => _ctx.Inventory;
    internal HardwareStatusManager _hardwareStatusManager => _ctx.HardwareStatusManager;
    internal DepositController _depositController => _ctx.DepositController;
    internal SimulatorContext Context => _ctx;

    /// <summary>シミュレータの依存関係を注入して初期化します。</summary>
    /// <remarks>各種マネージャーやコントローラーが未指定の場合は、デフォルトの実装を生成して使用します。</remarks>
    public SimulatorCashChanger(SimulatorDependencies deps)
    {
        _logger = LogProvider.CreateLogger<SimulatorCashChanger>();
        _eventNotifier = deps.EventNotifier ?? new UposEventNotifier(this);
        var hardwareStatusManager = deps.HardwareStatusManager ?? new HardwareStatusManager();
        var mediator = deps.Mediator as UposMediator ?? new UposMediator(this);
        var lifecycleManager = new LifecycleManager(hardwareStatusManager, mediator, _logger);
        
        var configProvider = deps.ConfigProvider ?? new ConfigurationProvider();
        var inventory = deps.Inventory ?? new Inventory();
        var history = deps.History ?? new TransactionHistory();
        var manager = deps.Manager ?? new CashChangerManager(inventory, history, new ChangeCalculator());

        var depositController = deps.DepositController ?? new DepositController(inventory, hardwareStatusManager, manager, configProvider);
        var dispenseController = deps.DispenseController ?? new DispenseController(manager, hardwareStatusManager, new HardwareSimulator(configProvider));

        var aggregator = deps.AggregatorProvider?.Aggregator ?? new OverallStatusAggregator(
            inventory.AllCounts
            .Select(kv => (kv.Key, Settings: configProvider.Config.GetDenominationSetting(kv.Key)))
            .Select(x => new CashStatusMonitor(inventory, x.Key, x.Settings.NearEmpty, x.Settings.NearFull, x.Settings.Full))
            .ToList());

        // Context を先に作成（StatusCoordinator 抜き）
        _ctx = new SimulatorContext
        {
            Inventory = inventory,
            History = history,
            Manager = manager,
            StatusAggregator = aggregator,
            HardwareStatusManager = hardwareStatusManager,
            DepositController = depositController,
            DispenseController = dispenseController,
            DiagnosticController = deps.DiagnosticController ?? new DiagnosticController(inventory, hardwareStatusManager),
            Mediator = mediator,
            LifecycleManager = lifecycleManager,
            StatusCoordinator = null! // 後で入れる
        };

        // StatusCoordinator は this.DeviceStatus を参照するため、_ctx が代入された後に作成する
        _ctx.StatusCoordinator = new StatusCoordinator(this, aggregator, hardwareStatusManager, depositController, dispenseController);
        _ctx.StatusCoordinator.Start();

        // ライフサイクルハンドラーを初期化（null参照防止）
        _ctx.LifecycleManager.UpdateHandler(_ctx.Mediator.SkipStateVerification);

        _configManager = deps.ConfigurationManager ?? new UposConfigurationManager(configProvider, inventory, this);
        _configManager.Initialize();

        _dispenseFacade = new UposDispenseFacade(_ctx.DispenseController, _ctx.DepositController, _ctx.HardwareStatusManager, _ctx.Inventory, _ctx.Mediator, _logger);
        _depositFacade = new DepositFacade(_ctx.DepositController, _ctx.Mediator, _ctx.DiagnosticController);
        _inventoryFacade = new InventoryFacade(_ctx.Inventory, _ctx.Manager, _ctx.Mediator);
        _diagnosticsFacade = new DiagnosticsFacade(_ctx.DiagnosticController, _ctx.Mediator);
        _capFacade = new CapabilitiesFacade(configProvider.Config);

        DevicePath = "SimulatorCashChanger";
    }

    // Lifecycle
    public override void Open() => _ctx.LifecycleManager.Open(base.Open);
    public override void Close() => _ctx.LifecycleManager.Close(base.Close);
    public override void Claim(int timeout) => _ctx.LifecycleManager.Claim(timeout, base.Claim);
    public override void Release() => _ctx.LifecycleManager.Release(base.Release);
    public override bool Claimed => _ctx.LifecycleManager.Claimed;
    public override ControlState State => _ctx.LifecycleManager.State;
    public override bool DeviceEnabled { get => _ctx.LifecycleManager.DeviceEnabled; set => _ctx.LifecycleManager.DeviceEnabled = value; }
    public override bool DataEventEnabled { get => _ctx.LifecycleManager.DataEventEnabled; set => _ctx.LifecycleManager.DataEventEnabled = value; }

    // Capabilities (Delegated)
    public override bool CapDeposit => _capFacade.CapDeposit;
    public override bool CapDepositDataEvent => _capFacade.CapDepositDataEvent;
    public override bool CapPauseDeposit => _capFacade.CapPauseDeposit;
    public override bool CapRepayDeposit => _capFacade.CapRepayDeposit;
    public virtual bool CapPurgeCash => _capFacade.CapPurgeCash;
    public override bool CapDiscrepancy => _capFacade.CapDiscrepancy;
    public override bool CapFullSensor => _capFacade.CapFullSensor;
    public override bool CapNearFullSensor => _capFacade.CapNearFullSensor;
    public override bool CapNearEmptySensor => _capFacade.CapNearEmptySensor;
    public override bool CapEmptySensor => _capFacade.CapEmptySensor;
    public override bool CapStatisticsReporting => _capFacade.CapStatisticsReporting;
    public override bool CapUpdateStatistics => _capFacade.CapUpdateStatistics;
    public override bool CapRealTimeData => _capFacade.CapRealTimeData;
    public override bool RealTimeDataEnabled { get => _depositFacade.RealTimeDataEnabled; set => _depositFacade.RealTimeDataEnabled = value; }

    // Core Operations
    public override void BeginDeposit() => _depositFacade.BeginDeposit();
    public override void EndDeposit(CashDepositAction action) => _depositFacade.EndDeposit(action);
    public override void FixDeposit() => _depositFacade.FixDeposit();
    public override void PauseDeposit(CashDepositPause control) => _depositFacade.PauseDeposit(control);
    public virtual void RepayDeposit() => _depositFacade.RepayDeposit();
    public override void DispenseChange(int amount) => _dispenseFacade.DispenseByAmount(amount, _configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode), AsyncMode, _ctx.Mediator.HandleDispenseResult);
    public override void DispenseCash(CashCount[] cashCounts) => _dispenseFacade.DispenseByCashCounts(cashCounts, _configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode), AsyncMode, _ctx.Mediator.HandleDispenseResult);
    public virtual void ClearOutput() => _dispenseFacade.ClearOutput();
    public override void AdjustCashCounts(IEnumerable<CashCount> cashCounts) => _inventoryFacade.AdjustCashCounts(cashCounts, _configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode), _ctx.HardwareStatusManager);
    public override CashCounts ReadCashCounts() => _inventoryFacade.ReadCashCounts(_configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode));
    public virtual void PurgeCash() => _inventoryFacade.PurgeCash();

    // Diagnostics & Stats
    public override string CheckHealth(HealthCheckLevel level) => _checkHealthText = _diagnosticsFacade.CheckHealth(level);
    public override string CheckHealthText => _checkHealthText;
    public override string RetrieveStatistics(string[] statistics) => _diagnosticsFacade.RetrieveStatistics(statistics);
    public override void UpdateStatistics(Statistic[] statistics) => _diagnosticsFacade.UpdateStatistics(statistics);
    public override void ResetStatistics(string[] statistics) => _diagnosticsFacade.ResetStatistics(statistics);
    
    // ICashChangerStatusSink Implementation
    void ICashChangerStatusSink.FireEvent(EventArgs e) => NotifyEvent(e);
    void ICashChangerStatusSink.SetAsyncProcessing(bool isBusy) => _eventNotifier.SetAsyncProcessing(isBusy);

    // Properties
    public override CashChangerStatus DeviceStatus => _ctx.LifecycleManager.State == ControlState.Closed ? CashChangerStatus.OK : _ctx.StatusCoordinator.LastCashChangerStatus;
    public override CashChangerFullStatus FullStatus => _ctx.LifecycleManager.State == ControlState.Closed ? CashChangerFullStatus.OK : _ctx.StatusCoordinator.LastFullStatus;
    
    public int ResultCode { get => _ctx.Mediator.ResultCode; set => _ctx.Mediator.SetFailure((ErrorCode)value); }
    public int ResultCodeExtended { get => _ctx.Mediator.ResultCodeExtended; set => _ctx.Mediator.SetFailure((ErrorCode)ResultCode, value); }

    public override bool AsyncMode { get; set; }
    public override int AsyncResultCode => _ctx.Mediator.AsyncResultCode;
    public override int AsyncResultCodeExtended => _ctx.Mediator.AsyncResultCodeExtended;
    public override string CurrencyCode { get => _configManager.CurrencyCode; set => _configManager.CurrencyCode = value; }
    public override string[] CurrencyCodeList => _configManager.CurrencyCodeList;
    public override string[] DepositCodeList => _configManager.DepositCodeList;
    public override CashUnits CurrencyCashList => _inventoryFacade.GetCashList(_configManager.CurrencyCode);
    public override CashUnits DepositCashList => CapDeposit ? _inventoryFacade.GetCashList(_configManager.CurrencyCode) : new CashUnits();
    public override CashUnits ExitCashList => _inventoryFacade.GetCashList(_configManager.CurrencyCode);
    public override int DepositAmount => _depositFacade.GetUposDepositAmount(UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode));
    public override CashCount[] DepositCounts => _depositFacade.GetUposDepositCounts(_configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode));
    public override CashDepositStatus DepositStatus => _depositFacade.DepositStatus;
    public override int CurrentExit { get => _capFacade.CurrentExit; set => _capFacade.CurrentExit = value; }
    public override int DeviceExits => _capFacade.DeviceExits;

    // DirectIO
    public override DirectIOData DirectIO(int command, int data, object obj) { _ctx.Mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true); var result = _directIOHandler.Handle(command, data, obj, this); _ctx.Mediator.SetSuccess(); return result; }

    // Event Sink Implementation
    void IUposEventSink.NotifyEvent(EventArgs e) => _eventNotifier.NotifyEvent(e);
    void IUposEventSink.QueueEvent(EventArgs e) => _eventNotifier.QueueEvent(e);
    void IUposEventSink.QueueDataEvent(DataEventArgs e) => QueueEvent(e);
    void IUposEventSink.QueueStatusUpdateEvent(StatusUpdateEventArgs e) => QueueEvent(e);
    protected virtual void NotifyEvent(EventArgs e) => _eventNotifier.NotifyEvent(e);
    bool IUposEventSink.DataEventEnabled => DataEventEnabled;
    bool IUposEventSink.CapDepositDataEvent => CapDepositDataEvent;
    bool IUposEventSink.SkipStateVerification => _ctx.Mediator.SkipStateVerification;
    bool IUposEventSink.RealTimeDataEnabled => RealTimeDataEnabled;

    // Infrastructure
    public override string DeviceName => "SimulatorCashChanger";
    public override string DeviceDescription => "Virtual Cash Changer Simulator";

    // Internal Helpers (for Extensions)
    internal void SetAsyncProcessingInternal(bool isBusy) => _ctx.Mediator.IsBusy = isBusy;
    internal void FireEventInternal(EventArgs e) => NotifyEvent(e);

    // IDisposable
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ctx.StatusCoordinator?.Dispose();
        }
        base.Dispose(disposing);
    }
    /// <inheritdoc/>
    public Observable<Unit> DepositChanged => _depositController.Changed;
}
