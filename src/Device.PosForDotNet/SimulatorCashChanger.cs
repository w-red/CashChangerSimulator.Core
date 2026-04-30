using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.PosForDotNet.Facades;
using CashChangerSimulator.Device.PosForDotNet.Models;
using CashChangerSimulator.Device.PosForDotNet.Services;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using R3;
using CoreDeviceEventTypes = CashChangerSimulator.Core.Services.DeviceEventTypes;
using IInternalUposEventSink = CashChangerSimulator.Device.PosForDotNet.Services.IUposEventSink;

namespace CashChangerSimulator.Device.PosForDotNet;

/// <summary>Microsoft POS for .NET (OPOS) に準拠した仮想現金入出金機のシミュレータクラス。</summary>
/// <remarks>
/// Microsoft Point of Service SDK を通じて、標準的な OPOS インターフェースを提供します。
/// 内部的には複数の Facade と Controller を使用して、在庫管理、入金処理、出金処理、および診断機能を提供し、
/// 物理デバイスなしでのアプリケーション開発とテストを支援します。
/// </remarks>
[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Virtual Cash Changer Simulator", 1, 14)]
public class SimulatorCashChanger : CashChangerBasic, IInternalUposEventSink, IDeviceStateProvider, ICashChangerStatusSink, ICashChangerDevice
{
    private readonly Subject<PosSharp.Abstractions.UposDataEventArgs> dataEvents = new();
    private readonly Subject<PosSharp.Abstractions.UposErrorEventArgs> errorEvents = new();
    private readonly Subject<PosSharp.Abstractions.UposStatusUpdateEventArgs> statusUpdateEvents = new();
    private readonly Subject<CoreDeviceEventTypes.DeviceDirectIOEventArgs> directIOEvents = new();
    private readonly Subject<PosSharp.Abstractions.UposOutputCompleteEventArgs> outputCompleteEvents = new();
    private readonly UposCashChangerCore core;
    private readonly DirectIOHandler directIOHandler = new();
    private string checkHealthText = "OK";
    private bool baseDisposed;

    /// <summary>オブジェクトが破棄済みかどうかを取得します。</summary>
    protected bool IsDisposed { get; private set; }

    /// <summary><see cref="SimulatorCashChanger"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="deps">シミュレータの依存関係。</param>
    public SimulatorCashChanger(SimulatorDependencies deps)
    {
        core = new UposCashChangerCore(deps, this);
        DevicePath = "SimulatorCashChanger";
    }

    /// <summary>ハードウェア状態マネージャーを取得します。</summary>
    public HardwareStatusManager HardwareStatus => core.Context.HardwareStatusManager;

    /// <summary>シミュレータのコンテキストを取得します。</summary>
    public SimulatorContext Context => core.Context;

    // --- Internal components for subclasses and strategies ---

    internal Inventory Inventory => core.Context.Inventory;
    internal HardwareStatusManager HardwareStatusManager => core.Context.HardwareStatusManager;
    internal DepositController DepositController => core.Context.DepositController;
    internal DispenseController DispenseController => core.Context.DispenseController;
    internal DiagnosticController DiagnosticController => core.Context.DiagnosticController;
    internal CashChangerManager Manager => core.Context.Manager;

    /// <inheritdoc/>
    public override bool Claimed => core.Context.LifecycleManager.Claimed;

    /// <inheritdoc/>
    public override ControlState State => core.Context.LifecycleManager.State;

    /// <inheritdoc/>
    public override bool DeviceEnabled { get => core.Context.LifecycleManager.DeviceEnabled; set => core.Context.LifecycleManager.DeviceEnabled = value; }

    /// <inheritdoc/>
    public override bool DataEventEnabled { get => core.Context.LifecycleManager.DataEventEnabled; set => core.Context.LifecycleManager.DataEventEnabled = value; }

    /// <inheritdoc/>
    public Observable<PosSharp.Abstractions.UposDataEventArgs> DataEvents => dataEvents;

    /// <inheritdoc/>
    public Observable<PosSharp.Abstractions.UposErrorEventArgs> ErrorEvents => errorEvents;

    /// <inheritdoc/>
    public Observable<PosSharp.Abstractions.UposStatusUpdateEventArgs> StatusUpdateEvents => statusUpdateEvents;

    /// <inheritdoc/>
    public Observable<CoreDeviceEventTypes.DeviceDirectIOEventArgs> DirectIOEvents => directIOEvents;

    /// <inheritdoc/>
    public Observable<PosSharp.Abstractions.UposOutputCompleteEventArgs> OutputCompleteEvents => outputCompleteEvents;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsBusy => core.Context.Mediator.IsBusyProperty;

    /// <inheritdoc/>
    ReadOnlyReactiveProperty<PosSharp.Abstractions.ControlState> ICashChangerDevice.State => core.StateProperty;

    // Capabilities (Delegated)

    /// <inheritdoc/>
    public override bool CapDeposit => CapabilitiesFacade.CapDeposit;

    /// <inheritdoc/>
    public override bool CapDepositDataEvent => CapabilitiesFacade.CapDepositDataEvent;

    /// <inheritdoc/>
    public override bool CapPauseDeposit => CapabilitiesFacade.CapPauseDeposit;

    /// <inheritdoc/>
    public override bool CapRepayDeposit => CapabilitiesFacade.CapRepayDeposit;

    /// <summary>キャッシュのパージ機能をサポートしているかどうかを取得します。</summary>
    public virtual bool CapPurgeCash => CapabilitiesFacade.CapPurgeCash;

    /// <inheritdoc/>
    public override bool CapDiscrepancy => CapabilitiesFacade.CapDiscrepancy;

    /// <inheritdoc/>
    public override bool CapFullSensor => CapabilitiesFacade.CapFullSensor;

    /// <inheritdoc/>
    public override bool CapNearFullSensor => CapabilitiesFacade.CapNearFullSensor;

    /// <inheritdoc/>
    public override bool CapNearEmptySensor => CapabilitiesFacade.CapNearEmptySensor;

    /// <inheritdoc/>
    public override bool CapEmptySensor => CapabilitiesFacade.CapEmptySensor;

    /// <inheritdoc/>
    public override bool CapStatisticsReporting => CapabilitiesFacade.CapStatisticsReporting;

    /// <inheritdoc/>
    public override bool CapUpdateStatistics => CapabilitiesFacade.CapUpdateStatistics;

    /// <inheritdoc/>
    public override bool CapRealTimeData => core.CapFacade.CapRealTimeData;

    /// <inheritdoc/>
    public override bool RealTimeDataEnabled { get => core.DepositFacade.RealTimeDataEnabled; set => core.DepositFacade.RealTimeDataEnabled = value; }

    /// <inheritdoc/>
    public override string CheckHealthText => checkHealthText;

    // Infrastructure Properties

    /// <inheritdoc/>
    public override string DeviceName => "SimulatorCashChanger";

    /// <inheritdoc/>
    public override string DeviceDescription => "Virtual Cash Changer Simulator";

    /// <inheritdoc/>
    public override Microsoft.PointOfService.CashChangerStatus DeviceStatus => core.StatusMonitor.DeviceStatus;

    /// <inheritdoc/>
    public override Microsoft.PointOfService.CashChangerFullStatus FullStatus => core.StatusMonitor.FullStatus;

    /// <inheritdoc/>
    public override bool AsyncMode { get; set; }

    /// <inheritdoc/>
    public override int AsyncResultCode => core.Context.Mediator.AsyncResultCode;

    /// <inheritdoc/>
    public override int AsyncResultCodeExtended => core.Context.Mediator.AsyncResultCodeExtended;

    /// <inheritdoc/>
    public override string CurrencyCode { get => core.ConfigManager.CurrencyCode; set => core.ConfigManager.CurrencyCode = value; }

    /// <inheritdoc/>
    public override string[] CurrencyCodeList => core.ConfigManager.CurrencyCodeList;

    /// <inheritdoc/>
    public override string[] DepositCodeList => core.ConfigManager.DepositCodeList;

    /// <inheritdoc/>
    public override CashUnits CurrencyCashList => core.InventoryFacade.GetCashList(core.ConfigManager.CurrencyCode);

    /// <inheritdoc/>
    public override CashUnits DepositCashList => CapDeposit ? core.InventoryFacade.GetCashList(core.ConfigManager.CurrencyCode) : default;

    /// <inheritdoc/>
    public override CashUnits ExitCashList => core.InventoryFacade.GetCashList(core.ConfigManager.CurrencyCode);

    /// <inheritdoc/>
    public override int DepositAmount => core.DepositFacade.GetUposDepositAmount(UposCurrencyHelper.GetCurrencyFactor(core.ConfigManager.CurrencyCode));

    /// <inheritdoc/>
    public override CashCount[] DepositCounts => core.DepositFacade.GetUposDepositCounts(core.ConfigManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(core.ConfigManager.CurrencyCode));

    /// <inheritdoc/>
    public override CashDepositStatus DepositStatus => core.DepositFacade.DepositStatus;

    /// <inheritdoc/>
    public override int CurrentExit { get => core.CapFacade.CurrentExit; set => core.CapFacade.CurrentExit = value; }

    /// <inheritdoc/>
    public override int DeviceExits => CapabilitiesFacade.DeviceExits;

    // Extra Public Properties (Delegated)

    /// <summary>直近の操作の実行結果を取得または設定します。</summary>
    public int ResultCode { get => core.Context.Mediator.ResultCode; set => core.Context.Mediator.SetFailure((ErrorCode)value); }

    /// <summary>直近の操作の拡張実行結果を取得または設定します。</summary>
    public int ResultCodeExtended { get => core.Context.Mediator.ResultCodeExtended; set => core.Context.Mediator.SetFailure((ErrorCode)ResultCode, value); }

    /// <summary>必要な入金金額を取得または設定します。</summary>
    public decimal RequiredAmount { get => core.Context.DepositController.RequiredAmount; set => core.Context.DepositController.RequiredAmount = value; }

    /// <summary>入金処理が進行中かどうかを取得します。</summary>
    public bool IsDepositInProgress => core.Context.DepositController.IsDepositInProgress;

    /// <summary>状態検証をスキップするかどうかを取得または設定します。</summary>
    public bool SkipStateVerification
    {
        get => core.Context.Mediator.SkipStateVerification;
        set
        {
            if (core.Context.Mediator.SkipStateVerification == value) return;
            core.Context.Mediator.SkipStateVerification = value;
            core.Context.LifecycleManager.UpdateHandler(value);
        }
    }

    /// <summary>入金状態の変更を通知するストリームを取得します。</summary>
    public Observable<Unit> DepositChanged => core.Context.DepositController.Changed;

    // Interface Implementation Properties (Delegated)
    bool ICashChangerStatusSink.Claimed { get => core.Context.Mediator.Claimed; set => core.Context.Mediator.Claimed = value; }
    bool IInternalUposEventSink.Claimed { get => core.Context.Mediator.Claimed; set => core.Context.Mediator.Claimed = value; }
    bool ICashChangerStatusSink.ClaimedByAnother { get => core.Context.HardwareStatusManager.IsClaimedByAnother.CurrentValue; set { /* Ignore */ } }
    bool IInternalUposEventSink.ClaimedByAnother { get => core.Context.HardwareStatusManager.IsClaimedByAnother.CurrentValue; set { /* Ignore */ } }
    bool ICashChangerStatusSink.DeviceEnabled { get => core.Context.Mediator.DeviceEnabled; set => core.Context.Mediator.DeviceEnabled = value; }
    bool IInternalUposEventSink.DeviceEnabled { get => core.Context.Mediator.DeviceEnabled; set => core.Context.Mediator.DeviceEnabled = value; }
    int ICashChangerStatusSink.AsyncResultCode { get => core.Context.Mediator.AsyncResultCode; set => core.Context.Mediator.AsyncResultCode = value; }
    int IInternalUposEventSink.AsyncResultCode { get => core.Context.Mediator.AsyncResultCode; set => core.Context.Mediator.AsyncResultCode = value; }
    int ICashChangerStatusSink.AsyncResultCodeExtended { get => core.Context.Mediator.AsyncResultCodeExtended; set => core.Context.Mediator.AsyncResultCodeExtended = value; }
    int IInternalUposEventSink.AsyncResultCodeExtended { get => core.Context.Mediator.AsyncResultCodeExtended; set => core.Context.Mediator.AsyncResultCodeExtended = value; }
    bool ICashChangerStatusSink.RealTimeDataEnabled => RealTimeDataEnabled;
    bool IInternalUposEventSink.DataEventEnabled => core.Context.Mediator.DataEventEnabled;
    bool IInternalUposEventSink.DisableUposEventQueuing => SkipStateVerification;
    ControlState IInternalUposEventSink.State => State;
    PosSharp.Abstractions.ControlState IDeviceStateProvider.State => InternalStatusMonitor.MapToControlState(State);

    // Lifecycle Methods (Delegated with base calls)

    /// <inheritdoc/>
    private bool isPosSdkOpened;

    /// <inheritdoc/>
    public override void Open()
    {
        core.Context.LifecycleManager.UpdateHandler(SkipStateVerification);
        core.Context.LifecycleManager.Open(() => 
        {
            base.Open();
            isPosSdkOpened = true;
        });
    }

    /// <inheritdoc/>
    public override void Close() => core.Context.LifecycleManager.Close(base.Close);

    /// <inheritdoc/>
    public override void Claim(int timeout) => core.Context.LifecycleManager.Claim(timeout, base.Claim);

    /// <inheritdoc/>
    public override void Release() => core.Context.LifecycleManager.Release(base.Release);

    public Task OpenAsync() => Task.Run(Open);
    public Task CloseAsync() => Task.Run(Close);
    public Task ClaimAsync(int timeout) => Task.Run(() => Claim(timeout));
    public Task ReleaseAsync() => Task.Run(Release);
    public Task EnableAsync() => Task.Run(() => DeviceEnabled = true);
    public Task DisableAsync() => Task.Run(() => DeviceEnabled = false);

    // Core Operations Methods (Delegated)

    /// <inheritdoc/>
    public override void BeginDeposit() => core.DepositFacade.BeginDeposit();

    /// <inheritdoc/>
    public override void EndDeposit(CashDepositAction successAction) => core.DepositFacade.EndDeposit(successAction);

    /// <inheritdoc/>
    public override void FixDeposit() => core.DepositFacade.FixDeposit();

    /// <inheritdoc/>
    public override void PauseDeposit(CashDepositPause pauseAction) => core.DepositFacade.PauseDeposit(pauseAction);

    public virtual void RepayDeposit() => core.DepositFacade.RepayDeposit();

    /// <inheritdoc/>
    public override void DispenseChange(int amount) => core.DispenseFacade.DispenseByAmount(amount, core.ConfigManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(core.ConfigManager.CurrencyCode), AsyncMode);

    /// <inheritdoc/>
    public override void DispenseCash(CashCount[] cashCounts) => core.DispenseFacade.DispenseByCashCounts(cashCounts, core.ConfigManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(core.ConfigManager.CurrencyCode), AsyncMode);

    public virtual void ClearOutput() => core.DispenseFacade.ClearOutput();

    public Task BeginDepositAsync() => Task.Run(BeginDeposit);
    public Task EndDepositAsync(DepositAction action)
    {
        var posAction = action switch
        {
            DepositAction.Repay => CashDepositAction.Repay,
            DepositAction.NoChange => CashDepositAction.NoChange,
            DepositAction.Change => CashDepositAction.Change,
            _ => throw new ArgumentException("Invalid deposit action", nameof(action))
        };
        return Task.Run(() => EndDeposit(posAction));
    }
    public Task FixDepositAsync() => Task.Run(FixDeposit);
    public Task PauseDepositAsync(DeviceDepositPause control)
    {
        var pauseAction = control switch
        {
            DeviceDepositPause.Pause => CashDepositPause.Pause,
            DeviceDepositPause.Resume => CashDepositPause.Restart,
            _ => throw new ArgumentException("Invalid pause control", nameof(control))
        };
        return Task.Run(() => PauseDeposit(pauseAction));
    }
    public Task RepayDepositAsync() => Task.Run(RepayDeposit);
    public Task DispenseChangeAsync(int amount) => Task.Run(() => DispenseChange(amount));
    public Task DispenseCashAsync(IEnumerable<CashDenominationCount> counts)
    {
        var posCounts = counts.Select(c => UposCurrencyHelper.ToCashCount(c, core.Context.Inventory.AllCounts.Select(kv => kv.Key))).ToArray();
        return Task.Run(() => DispenseCash(posCounts));
    }
    public Task<Inventory> ReadInventoryAsync() => Task.FromResult(core.Context.Inventory);
    public Task AdjustInventoryAsync(IEnumerable<CashDenominationCount> counts)
    {
        var posCounts = counts.Select(c => UposCurrencyHelper.ToCashCount(c, core.Context.Inventory.AllCounts.Select(kv => kv.Key))).ToArray();
        return Task.Run(() => AdjustCashCounts(posCounts));
    }
    public Task PurgeCashAsync() => Task.Run(PurgeCash);
    public Task<string> CheckHealthAsync(PosSharp.Abstractions.HealthCheckLevel level) => Task.Run(() => CheckHealth((HealthCheckLevel)level));

    /// <inheritdoc/>
    public override void AdjustCashCounts(IEnumerable<CashCount> cashCounts) => core.InventoryFacade.AdjustCashCounts(cashCounts, core.ConfigManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(core.ConfigManager.CurrencyCode), core.Context.HardwareStatusManager);

    public void AdjustCashCounts(string cashCounts) => core.InventoryFacade.AdjustCashCounts(cashCounts, core.ConfigManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(core.ConfigManager.CurrencyCode), core.Context.HardwareStatusManager);

    /// <inheritdoc/>
    public override CashCounts ReadCashCounts() => core.InventoryFacade.ReadCashCounts(core.ConfigManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(core.ConfigManager.CurrencyCode));

    public void ReadCashCounts(ref string cashCounts, ref bool discrepancy)
    {
        var result = ReadCashCounts();
        cashCounts = CashCountAdapter.FormatCashCounts(result.Counts);
        discrepancy = result.Discrepancy;
    }

    public virtual void PurgeCash() => core.InventoryFacade.PurgeCash();

    // Diagnostics & Stats (Delegated)
    /// <inheritdoc/>
    public override string CheckHealth(HealthCheckLevel level) => checkHealthText = core.DiagnosticsFacade.CheckHealth(level);
    /// <inheritdoc/>
    public override string RetrieveStatistics(string[] statistics) => core.DiagnosticsFacade.RetrieveStatistics(statistics);
    /// <inheritdoc/>
    public override void UpdateStatistics(Statistic[] statistics) => core.DiagnosticsFacade.UpdateStatistics(statistics);
    /// <inheritdoc/>
    public override void ResetStatistics(string[] statistics) => core.DiagnosticsFacade.ResetStatistics(statistics);

    // DirectIO (Delegated)
    /// <inheritdoc/>
    public override DirectIOData DirectIO(int command, int data, object obj)
    {
        core.Context.Mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
        var result = directIOHandler.Handle(command, data, obj, this);
        core.Context.Mediator.SetSuccess();
        return result;
    }

    /// <inheritdoc/>
    public Task<int> DirectIOAsync(int command, int data, object obj)
    {
        return Task.Run(() =>
        {
            var result = DirectIO(command, data, obj);
            return result.Data;
        });
    }

    // Event Management (Delegated)
    /// <inheritdoc/>
    public void FireEvent(EventArgs e) { if (!IsDisposed) NotifyEvent(e); }
    /// <inheritdoc/>
    public void SetAsyncProcessing(bool isBusy) => core.EventNotifier.SetAsyncProcessing(isBusy);

    internal void FireEventInternal(EventArgs e) => FireEvent(e);
    internal void SetAsyncProcessingInternal(bool isBusy) => SetAsyncProcessing(isBusy);

    void IInternalUposEventSink.NotifyEvent(EventArgs e) { if (!IsDisposed) NotifyEvent(e); }
    void IInternalUposEventSink.QueueEvent(EventArgs e)
    {
        if (IsDisposed) return;
        if (e is DataEventArgs de) { QueueEvent(de); }
        else if (e is StatusUpdateEventArgs se) { QueueEvent(se); }
        else if (e is DirectIOEventArgs die) { QueueEvent(die); }
    }
    public void QueueDataEvent(DataEventArgs e) => QueueEvent(e);
    public void QueueStatusUpdateEvent(StatusUpdateEventArgs e) => QueueEvent(e);

    protected virtual void NotifyEvent(EventArgs e)
    {
        if (IsDisposed) return;
        ((IInternalUposEventSink)this).QueueEvent(e);

        if (e is DataEventArgs de) dataEvents.OnNext(new PosSharp.Abstractions.UposDataEventArgs(de.Status));
        else if (e is DeviceErrorEventArgs ee) errorEvents.OnNext(new PosSharp.Abstractions.UposErrorEventArgs((PosSharp.Abstractions.UposErrorCode)ee.ErrorCode, ee.ErrorCodeExtended, (PosSharp.Abstractions.UposErrorLocus)ee.ErrorLocus, (PosSharp.Abstractions.UposErrorResponse)ee.ErrorResponse));
        else if (e is StatusUpdateEventArgs se) statusUpdateEvents.OnNext(new PosSharp.Abstractions.UposStatusUpdateEventArgs(se.Status));
        else if (e is DirectIOEventArgs die) directIOEvents.OnNext(new CoreDeviceEventTypes.DeviceDirectIOEventArgs(die.EventNumber, die.Data, die.Object));
        else if (e is OutputCompleteEventArgs oce) outputCompleteEvents.OnNext(new PosSharp.Abstractions.UposOutputCompleteEventArgs(oce.OutputId));

        core.StateProperty.Value = InternalStatusMonitor.MapToControlState(State);
    }

    // IDisposable Implementation
    protected override void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;
        if (disposing) core.Dispose();

        if (!baseDisposed)
        {
            try 
            { 
                if (isPosSdkOpened)
                {
                    base.Dispose(disposing); 
                }
                else
                {
                    // SDK has not been initialized. Calling base.Dispose() here causes a fatal NullReferenceException (CSE) in StopListeningForGlobalEvents.
                    // To prevent test runner crashes, we skip base cleanup for uninitialized devices.
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SimulatorCashChanger] POS SDK Internal Dispose Error: {ex.Message}"); }
            finally { baseDisposed = true; }
        }
    }
}
