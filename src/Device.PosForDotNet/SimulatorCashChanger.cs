using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using CashChangerSimulator.Device.PosForDotNet.Facades;
using CashChangerSimulator.Device.PosForDotNet.Models;
using CashChangerSimulator.Device.PosForDotNet.Services;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using R3;
using ZLogger;
using CoreDeviceEventTypes = CashChangerSimulator.Core.Services.DeviceEventTypes;

namespace CashChangerSimulator.Device.PosForDotNet;

/// <summary>Microsoft POS for .NET (OPOS) に準拠した仮想現金入出金機のシミュレータクラス。</summary>
/// <remarks>
/// Microsoft Point of Service SDK を通じて、標準的な OPOS インターフェースを提供します。
/// 内部的には複数の Facade と Controller を使用して、在庫管理、入金処理、出金処理、および診断機能を提供し、
/// 物理デバイスなしでのアプリケーション開発とテストを支援します。
/// </remarks>
[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Virtual Cash Changer Simulator", 1, 14)]
public class SimulatorCashChanger : CashChangerBasic, IUposEventSink, IDeviceStateProvider, ICashChangerStatusSink, ICashChangerDevice, IDisposable
{
    private readonly Subject<CoreDeviceEventTypes.DeviceDataEventArgs> dataEvents = new();
    private readonly Subject<CoreDeviceEventTypes.DeviceErrorEventArgs> errorEvents = new();
    private readonly Subject<CoreDeviceEventTypes.DeviceStatusUpdateEventArgs> statusUpdateEvents = new();
    private readonly Subject<CoreDeviceEventTypes.DeviceDirectIOEventArgs> directIOEvents = new();
    private readonly Subject<CoreDeviceEventTypes.DeviceOutputCompleteEventArgs> outputCompleteEvents = new();
    private readonly ReactiveProperty<DeviceControlState> stateProperty;
    private readonly CompositeDisposable disposables = new();

    protected readonly SimulatorContext ctx;
    private readonly ILogger<SimulatorCashChanger> logger;
    private readonly UposDispenseFacade dispenseFacade;
    private readonly DepositFacade depositFacade;
    private readonly InventoryFacade inventoryFacade;
    private readonly DiagnosticsFacade diagnosticsFacade;
    private readonly CapabilitiesFacade capFacade;
    private readonly UposConfigurationManager configManager;
    private readonly UposEventNotifier eventNotifier;
    private readonly DirectIOHandler directIOHandler = new();
    private string checkHealthText = "OK";
    private bool disposedValue;

    /// <summary><see cref="SimulatorCashChanger"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="deps">シミュレータの依存関係。</param>
    /// <remarks>各種マネージャーやコントローラーが未指定の場合は、デフォルトの実装を生成して使用します。</remarks>
    public SimulatorCashChanger(SimulatorDependencies deps)
    {
        ArgumentNullException.ThrowIfNull(deps);
        logger = LogProvider.CreateLogger<SimulatorCashChanger>();
        ctx = SimulatorContext.Create(deps, this, logger);
        eventNotifier = (UposEventNotifier)ctx.EventNotifier;
        ctx.StatusCoordinator.Start();

        // ライフサイクルハンドラーを初期化（null参照防止）
        ctx.LifecycleManager.UpdateHandler(ctx.Mediator.SkipStateVerification);

        var configProvider = deps.ConfigProvider ?? new ConfigurationProvider();
        configManager = deps.ConfigurationManager ?? new UposConfigurationManager(configProvider, Inventory, this);
        configManager.Initialize();

        dispenseFacade = new UposDispenseFacade(ctx.DispenseController, ctx.DepositController, ctx.HardwareStatusManager, ctx.Inventory, ctx.Mediator, logger);
        depositFacade = new DepositFacade(ctx.DepositController, ctx.Mediator, ctx.DiagnosticController);
        inventoryFacade = new InventoryFacade(ctx.Inventory, ctx.Manager, ctx.Mediator);
        diagnosticsFacade = new DiagnosticsFacade(ctx.DiagnosticController, ctx.Mediator);
        capFacade = new CapabilitiesFacade(configProvider.Config);

        DevicePath = "SimulatorCashChanger";

        // Subscribe to relevant state changes (example for AddTo pattern)
        ctx.HardwareStatusManager.IsConnected
            .Subscribe(v => logger.ZLogDebug($"Hardware connection: {v}"))
            .AddTo(disposables);
 
        stateProperty = new ReactiveProperty<DeviceControlState>(MapToDeviceControlState(State)).AddTo(disposables);
    }

    /// <summary>ハードウェア状態マネージャーを取得します。</summary>
    public HardwareStatusManager HardwareStatus => HardwareStatusManager;

    /// <summary>シミュレータのコンテキストを取得します。</summary>
    public SimulatorContext Context => ctx;

    /// <inheritdoc/>
    public override bool Claimed => ctx.LifecycleManager.Claimed;

    /// <inheritdoc/>
    public override ControlState State => ctx.LifecycleManager.State;

    /// <inheritdoc/>
    public override bool DeviceEnabled { get => ctx.LifecycleManager.DeviceEnabled; set => ctx.LifecycleManager.DeviceEnabled = value; }

    /// <inheritdoc/>
    public override bool DataEventEnabled { get => ctx.LifecycleManager.DataEventEnabled; set => ctx.LifecycleManager.DataEventEnabled = value; }

    /// <inheritdoc/>
    public Observable<CoreDeviceEventTypes.DeviceDataEventArgs> DataEvents => dataEvents;
 
    /// <inheritdoc/>
    public Observable<CoreDeviceEventTypes.DeviceErrorEventArgs> ErrorEvents => errorEvents;
 
    /// <inheritdoc/>
    public Observable<CoreDeviceEventTypes.DeviceStatusUpdateEventArgs> StatusUpdateEvents => statusUpdateEvents;
 
    /// <inheritdoc/>
    public Observable<CoreDeviceEventTypes.DeviceDirectIOEventArgs> DirectIOEvents => directIOEvents;
 
    /// <inheritdoc/>
    public Observable<CoreDeviceEventTypes.DeviceOutputCompleteEventArgs> OutputCompleteEvents => outputCompleteEvents;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsBusy => ctx.Mediator.IsBusyProperty;

    /// <inheritdoc/>
    ReadOnlyReactiveProperty<DeviceControlState> ICashChangerDevice.State => stateProperty;

    // Capabilities (Delegated)
    /// <inheritdoc/>
    public override bool CapDeposit => capFacade.CapDeposit;

    /// <inheritdoc/>
    public override bool CapDepositDataEvent => capFacade.CapDepositDataEvent;

    /// <inheritdoc/>
    public override bool CapPauseDeposit => capFacade.CapPauseDeposit;

    /// <inheritdoc/>
    public override bool CapRepayDeposit => capFacade.CapRepayDeposit;

    /// <inheritdoc/>
    public virtual bool CapPurgeCash => capFacade.CapPurgeCash;

    /// <inheritdoc/>
    public override bool CapDiscrepancy => capFacade.CapDiscrepancy;

    /// <inheritdoc/>
    public override bool CapFullSensor => capFacade.CapFullSensor;

    /// <inheritdoc/>
    public override bool CapNearFullSensor => capFacade.CapNearFullSensor;

    /// <inheritdoc/>
    public override bool CapNearEmptySensor => capFacade.CapNearEmptySensor;

    /// <inheritdoc/>
    public override bool CapEmptySensor => capFacade.CapEmptySensor;

    /// <inheritdoc/>
    public override bool CapStatisticsReporting => capFacade.CapStatisticsReporting;

    /// <inheritdoc/>
    public override bool CapUpdateStatistics => capFacade.CapUpdateStatistics;

    /// <inheritdoc/>
    public override bool CapRealTimeData => capFacade.CapRealTimeData;

    /// <inheritdoc/>
    public override bool RealTimeDataEnabled { get => depositFacade.RealTimeDataEnabled; set => depositFacade.RealTimeDataEnabled = value; }

    /// <inheritdoc/>
    public override string CheckHealthText => checkHealthText;

    // Infrastructure Properties
    /// <inheritdoc/>
    public override string DeviceName => "SimulatorCashChanger";

    /// <inheritdoc/>
    public override string DeviceDescription => "Virtual Cash Changer Simulator";

    /// <inheritdoc/>
    public override CashChangerStatus DeviceStatus => ctx.LifecycleManager.State == ControlState.Closed ? CashChangerStatus.OK : ctx.StatusCoordinator.LastCashChangerStatus;

    /// <inheritdoc/>
    public override CashChangerFullStatus FullStatus => ctx.LifecycleManager.State == ControlState.Closed ? CashChangerFullStatus.OK : ctx.StatusCoordinator.LastFullStatus;

    /// <inheritdoc/>
    public override bool AsyncMode { get; set; }

    /// <inheritdoc/>
    public override int AsyncResultCode => ctx.Mediator.AsyncResultCode;

    /// <inheritdoc/>
    public override int AsyncResultCodeExtended => ctx.Mediator.AsyncResultCodeExtended;

    /// <inheritdoc/>
    public override string CurrencyCode { get => configManager.CurrencyCode; set => configManager.CurrencyCode = value; }

    /// <inheritdoc/>
    public override string[] CurrencyCodeList => configManager.CurrencyCodeList;

    /// <inheritdoc/>
    public override string[] DepositCodeList => configManager.DepositCodeList;

    /// <inheritdoc/>
    public override CashUnits CurrencyCashList => inventoryFacade.GetCashList(configManager.CurrencyCode);

    /// <inheritdoc/>
    public override CashUnits DepositCashList => CapDeposit ? inventoryFacade.GetCashList(configManager.CurrencyCode) : default(CashUnits);

    /// <inheritdoc/>
    public override CashUnits ExitCashList => inventoryFacade.GetCashList(configManager.CurrencyCode);

    /// <inheritdoc/>
    public override int DepositAmount => depositFacade.GetUposDepositAmount(UposCurrencyHelper.GetCurrencyFactor(configManager.CurrencyCode));

    /// <inheritdoc/>
    public override CashCount[] DepositCounts => depositFacade.GetUposDepositCounts(configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(configManager.CurrencyCode));

    /// <inheritdoc/>
    public override CashDepositStatus DepositStatus => depositFacade.DepositStatus;

    /// <inheritdoc/>
    public override int CurrentExit { get => capFacade.CurrentExit; set => capFacade.CurrentExit = value; }

    /// <inheritdoc/>
    public override int DeviceExits => capFacade.DeviceExits;

    // Extra Public Properties
    /// <inheritdoc/>
    public int ResultCode { get => ctx.Mediator.ResultCode; set => ctx.Mediator.SetFailure((ErrorCode)value); }

    /// <inheritdoc/>
    public int ResultCodeExtended { get => ctx.Mediator.ResultCodeExtended; set => ctx.Mediator.SetFailure((ErrorCode)ResultCode, value); }

    /// <inheritdoc/>
    public decimal RequiredAmount { get => DepositController.RequiredAmount; set => DepositController.RequiredAmount = value; }

    /// <inheritdoc/>
    public bool IsDepositInProgress => DepositController.IsDepositInProgress;

    /// <summary>状態検証（Open, Claim 等のシーケンスチェック）をスキップするかどうかを取得または設定します。</summary>
    /// <remarks>シミュレータとしての利便性を優先する場合に true に設定します。</remarks>
    public bool SkipStateVerification
    {
        get => ctx.Mediator.SkipStateVerification;
        set
        {
            if (ctx.Mediator.SkipStateVerification == value)
            {
                return;
            }

            ctx.Mediator.SkipStateVerification = value;
            ctx.LifecycleManager.UpdateHandler(value);
        }
    }

    /// <summary>入金状態の変更を通知するストリームを取得します。</summary>
    public Observable<Unit> DepositChanged => DepositController.Changed;

    internal Inventory Inventory => ctx.Inventory;

    internal HardwareStatusManager HardwareStatusManager => ctx.HardwareStatusManager;

    internal DepositController DepositController => ctx.DepositController;

    // Interface Implementation Properties
    bool ICashChangerStatusSink.Claimed { get => ctx.Mediator.Claimed; set => ctx.Mediator.Claimed = value; }

    bool IUposEventSink.Claimed { get => ctx.Mediator.Claimed; set => ctx.Mediator.Claimed = value; }

    bool ICashChangerStatusSink.ClaimedByAnother
    {
        get => ctx.HardwareStatusManager.IsClaimedByAnother.Value; set { /* No-op */ }
    }

    bool IUposEventSink.ClaimedByAnother
    {
        get => ctx.HardwareStatusManager.IsClaimedByAnother.Value; set { /* No-op */ }
    }

    bool ICashChangerStatusSink.DeviceEnabled { get => ctx.Mediator.DeviceEnabled; set => ctx.Mediator.DeviceEnabled = value; }

    bool IUposEventSink.DeviceEnabled { get => ctx.Mediator.DeviceEnabled; set => ctx.Mediator.DeviceEnabled = value; }

    int ICashChangerStatusSink.AsyncResultCode
    {
        get => ctx.Mediator.AsyncResultCode;
        set => ctx.Mediator.AsyncResultCode = value;
    }

    int IUposEventSink.AsyncResultCode
    {
        get => ctx.Mediator.AsyncResultCode;
        set => ctx.Mediator.AsyncResultCode = value;
    }

    int ICashChangerStatusSink.AsyncResultCodeExtended
    {
        get => ctx.Mediator.AsyncResultCodeExtended;
        set => ctx.Mediator.AsyncResultCodeExtended = value;
    }

    int IUposEventSink.AsyncResultCodeExtended
    {
        get => ctx.Mediator.AsyncResultCodeExtended;
        set => ctx.Mediator.AsyncResultCodeExtended = value;
    }

    bool ICashChangerStatusSink.RealTimeDataEnabled => RealTimeDataEnabled;

    bool IUposEventSink.DataEventEnabled => ctx.Mediator.DataEventEnabled;

    bool IUposEventSink.DisableUposEventQueuing => SkipStateVerification;

    ControlState IUposEventSink.State => State;

    DeviceControlState IDeviceStateProvider.State => MapToDeviceControlState(State);

    private static DeviceControlState MapToDeviceControlState(ControlState state) => state switch
    {
        ControlState.Busy => DeviceControlState.Busy,
        ControlState.Closed => DeviceControlState.Closed,
        ControlState.Error => DeviceControlState.Error,
        ControlState.Idle => DeviceControlState.Idle,
        _ => DeviceControlState.Error
    };

    // Lifecycle Methods
    /// <inheritdoc/>
    public override void Open()
    {
        ctx.LifecycleManager.UpdateHandler(SkipStateVerification);
        ctx.LifecycleManager.Open(base.Open);
    }

    /// <inheritdoc/>
    public override void Close() => ctx.LifecycleManager.Close(base.Close);

    /// <inheritdoc/>
    public override void Claim(int timeout) => ctx.LifecycleManager.Claim(timeout, base.Claim);

    /// <inheritdoc/>
    public override void Release() => ctx.LifecycleManager.Release(base.Release);

    /// <inheritdoc/>
    public Task OpenAsync() => Task.Run(Open);

    /// <inheritdoc/>
    public Task CloseAsync() => Task.Run(Close);

    /// <inheritdoc/>
    public Task ClaimAsync(int timeout) => Task.Run(() => Claim(timeout));

    /// <inheritdoc/>
    public Task ReleaseAsync() => Task.Run(Release);

    /// <inheritdoc/>
    public Task EnableAsync() => Task.Run(() => DeviceEnabled = true);

    /// <inheritdoc/>
    public Task DisableAsync() => Task.Run(() => DeviceEnabled = false);

    // Core Operations Methods
    /// <inheritdoc/>
    public override void BeginDeposit() => depositFacade.BeginDeposit();

    /// <inheritdoc/>
    public override void EndDeposit(CashDepositAction action) => depositFacade.EndDeposit(action);

    /// <inheritdoc/>
    public override void FixDeposit() => depositFacade.FixDeposit();

    /// <inheritdoc/>
    public override void PauseDeposit(CashDepositPause control) => depositFacade.PauseDeposit(control);

    /// <inheritdoc/>
    public virtual void RepayDeposit() => depositFacade.RepayDeposit();

    /// <inheritdoc/>
    public override void DispenseChange(int amount) => dispenseFacade.DispenseByAmount(amount, configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(configManager.CurrencyCode), AsyncMode);

    /// <inheritdoc/>
    public override void DispenseCash(CashCount[] cashCounts) => dispenseFacade.DispenseByCashCounts(cashCounts, configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(configManager.CurrencyCode), AsyncMode);

    /// <inheritdoc/>
    public virtual void ClearOutput() => dispenseFacade.ClearOutput();

    /// <inheritdoc/>
    public Task BeginDepositAsync() => Task.Run(BeginDeposit);

    /// <inheritdoc/>
    public Task EndDepositAsync(DepositAction action) => Task.Run(() => EndDeposit((CashDepositAction)action));

    /// <inheritdoc/>
    public Task FixDepositAsync() => Task.Run(FixDeposit);

    /// <inheritdoc/>
    public Task PauseDepositAsync(DeviceDepositPause control) => Task.Run(() => PauseDeposit((CashDepositPause)control));

    /// <inheritdoc/>
    public Task RepayDepositAsync() => Task.Run(RepayDeposit);

    /// <inheritdoc/>
    public Task DispenseChangeAsync(int amount) => Task.Run(() => DispenseChange(amount));

    /// <inheritdoc/>
    public Task DispenseCashAsync(IEnumerable<CashDenominationCount> counts)
    {
        var posCounts = counts.Select(c => UposCurrencyHelper.ToCashCount(c, Inventory.AllCounts.Select(kv => kv.Key))).ToArray();
        return Task.Run(() => DispenseCash(posCounts));
    }

    /// <inheritdoc/>
    public Task<Inventory> ReadInventoryAsync() => Task.FromResult(Inventory);

    /// <inheritdoc/>
    public Task AdjustInventoryAsync(IEnumerable<CashDenominationCount> counts)
    {
        var posCounts = counts.Select(c => UposCurrencyHelper.ToCashCount(c, Inventory.AllCounts.Select(kv => kv.Key))).ToArray();
        return Task.Run(() => AdjustCashCounts(posCounts));
    }

    /// <inheritdoc/>
    public Task PurgeCashAsync() => Task.Run(PurgeCash);

    /// <inheritdoc/>
    public Task<string> CheckHealthAsync(DeviceHealthCheckLevel level) => Task.Run(() => CheckHealth((HealthCheckLevel)level));

    /// <inheritdoc/>
    public override void AdjustCashCounts(IEnumerable<CashCount> cashCounts)
    {
        ArgumentNullException.ThrowIfNull(cashCounts);
        inventoryFacade.AdjustCashCounts(cashCounts, configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(configManager.CurrencyCode), ctx.HardwareStatusManager);
    }

    /// <inheritdoc/>
    public override CashCounts ReadCashCounts() => inventoryFacade.ReadCashCounts(configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(configManager.CurrencyCode));

    /// <inheritdoc/>
    public void AdjustCashCounts(string cashCounts) => inventoryFacade.AdjustCashCounts(cashCounts, configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(configManager.CurrencyCode), ctx.HardwareStatusManager);

    /// <inheritdoc/>
    public void ReadCashCounts(ref string cashCounts, ref bool discrepancy)
    {
        var result = ReadCashCounts();
        cashCounts = CashCountAdapter.FormatCashCounts(result.Counts);
        discrepancy = result.Discrepancy;
    }

    /// <inheritdoc/>
    public virtual void PurgeCash() => inventoryFacade.PurgeCash();

    // Diagnostics & Stats Methods
    /// <inheritdoc/>
    public override string CheckHealth(HealthCheckLevel level) => checkHealthText = diagnosticsFacade.CheckHealth(level);

    /// <inheritdoc/>
    public override string RetrieveStatistics(string[] statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        return diagnosticsFacade.RetrieveStatistics(statistics);
    }

    /// <inheritdoc/>
    public override void UpdateStatistics(Statistic[] statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        diagnosticsFacade.UpdateStatistics(statistics);
    }

    /// <inheritdoc/>
    public override void ResetStatistics(string[] statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        diagnosticsFacade.ResetStatistics(statistics);
    }

    // DirectIO Method
    /// <inheritdoc/>
    public override DirectIOData DirectIO(int command, int data, object obj)
    {
        ctx.Mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
        var result = directIOHandler.Handle(command, data, obj, this);
        ctx.Mediator.SetSuccess();
        return result;
    }

    // Event Management Methods
    /// <inheritdoc/>
    public void FireEvent(EventArgs e)
    {
        if (disposedValue)
        {
            return;
        }

        NotifyEvent(e);
        if (!SkipStateVerification)
        {
            if (e is DataEventArgs de)
            {
                QueueEvent(de);
            }
            else if (e is StatusUpdateEventArgs se)
            {
                QueueEvent(se);
            }
            else if (e is DirectIOEventArgs die)
            {
                QueueEvent(die);
            }
        }
    }

    /// <inheritdoc/>
    public void SetAsyncProcessing(bool isBusy) => eventNotifier.SetAsyncProcessing(isBusy);

    void IUposEventSink.NotifyEvent(EventArgs e)
    {
        if (disposedValue)
        {
            return;
        }

        NotifyEvent(e);
        if (!SkipStateVerification && !ctx.EventNotifier.DisableUposEventQueuing)
        {
            if (e is DataEventArgs de)
            {
                QueueEvent(de);
            }
            else if (e is StatusUpdateEventArgs se)
            {
                QueueEvent(se);
            }
            else if (e is DirectIOEventArgs die)
            {
                QueueEvent(die);
            }
        }
    }

    void IUposEventSink.QueueEvent(EventArgs e)
    {
        if (disposedValue)
        {
            return;
        }

        if (e is DataEventArgs de)
        {
            QueueEvent(de);
        }
        else if (e is StatusUpdateEventArgs se)
        {
            QueueEvent(se);
        }
        else if (e is DirectIOEventArgs die)
        {
            QueueEvent(die);
        }
    }

    /// <inheritdoc/>
    public void QueueDataEvent(DataEventArgs e) => QueueEvent(e);

    /// <inheritdoc/>
    public void QueueStatusUpdateEvent(StatusUpdateEventArgs se) => QueueEvent(se);

    /// <inheritdoc/>
    protected virtual void NotifyEvent(EventArgs e)
    {
        if (disposedValue)
        {
            return;
        }

        // Trigger native POS for .NET events using QueueEvent (via IUposEventSink implementation)
        ((IUposEventSink)this).QueueEvent(e);
 
        // Re-emit as abstracted events
        if (e is Microsoft.PointOfService.DataEventArgs de)
        {
            dataEvents.OnNext(new CoreDeviceEventTypes.DeviceDataEventArgs(de.Status));
        }
        else if (e is Microsoft.PointOfService.DeviceErrorEventArgs ee)
        {
            errorEvents.OnNext(new CoreDeviceEventTypes.DeviceErrorEventArgs(
                (DeviceErrorCode)ee.ErrorCode,
                ee.ErrorCodeExtended,
                (DeviceErrorLocus)ee.ErrorLocus,
                (DeviceErrorResponse)ee.ErrorResponse));
        }
        else if (e is Microsoft.PointOfService.StatusUpdateEventArgs se)
        {
            statusUpdateEvents.OnNext(new CoreDeviceEventTypes.DeviceStatusUpdateEventArgs(se.Status));
        }
        else if (e is Microsoft.PointOfService.DirectIOEventArgs die)
        {
            directIOEvents.OnNext(new CoreDeviceEventTypes.DeviceDirectIOEventArgs(die.EventNumber, die.Data, die.Object));
        }
        else if (e is Microsoft.PointOfService.OutputCompleteEventArgs oce)
        {
            outputCompleteEvents.OnNext(new CoreDeviceEventTypes.DeviceOutputCompleteEventArgs(oce.OutputId));
        }
 
        stateProperty.Value = MapToDeviceControlState(State);
    }

    // Internal Helpers Methods
    internal void SetAsyncProcessingInternal(bool isBusy) => ctx.Mediator.IsBusy = isBusy;
    internal void FireEventInternal(EventArgs e) => NotifyEvent(e);

    // IDisposable Implementation
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            return;
        }

        disposedValue = true;
        if (disposing)
        {
            try
            {
                ctx?.Dispose();
            }
            catch
            {
            }
        }

        try
        {
            if (State != ControlState.Closed)
            {
                try
                {
                    if (DeviceEnabled)
{
    DeviceEnabled = false;
}
                }
                catch
                {
                }

                if (CapRealTimeData)
                {
                    try
                    {
                        RealTimeDataEnabled = false;
                    }
                    catch
                    {
                    }
                }

                base.Dispose(disposing);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SimulatorCashChanger] Dispose SDK Error: {ex}");
        }
    }
}
