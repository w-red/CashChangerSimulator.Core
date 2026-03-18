using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.Device.Facades;
using CashChangerSimulator.Device.Models;
using R3;
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

    internal Inventory Inventory => _ctx.Inventory;
    internal HardwareStatusManager HardwareStatusManager => _ctx.HardwareStatusManager;
    public HardwareStatusManager HardwareStatus => HardwareStatusManager;
    internal DepositController DepositController => _ctx.DepositController;
    internal SimulatorContext Context => _ctx;

    /// <summary>シミュレータの依存関係を注入して初期化します。</summary>
    /// <remarks>各種マネージャーやコントローラーが未指定の場合は、デフォルトの実装を生成して使用します。</remarks>
    public SimulatorCashChanger(SimulatorDependencies deps)
    {
        ArgumentNullException.ThrowIfNull(deps);
        _logger = LogProvider.CreateLogger<SimulatorCashChanger>();
        _ctx = SimulatorContext.Create(deps, this, _logger);
        _eventNotifier = _ctx.EventNotifier;
        _ctx.StatusCoordinator.Start();

        // ライフサイクルハンドラーを初期化（null参照防止）
        _ctx.LifecycleManager.UpdateHandler(_ctx.Mediator.SkipStateVerification);

        var configProvider = deps.ConfigProvider ?? new ConfigurationProvider();
        _configManager = deps.ConfigurationManager ?? new UposConfigurationManager(configProvider, Inventory, this);
        _configManager.Initialize();

        _dispenseFacade = new UposDispenseFacade(_ctx.DispenseController, _ctx.DepositController, _ctx.HardwareStatusManager, _ctx.Inventory, _ctx.Mediator, _logger);
        _depositFacade = new DepositFacade(_ctx.DepositController, _ctx.Mediator, _ctx.DiagnosticController);
        _inventoryFacade = new InventoryFacade(_ctx.Inventory, _ctx.Manager, _ctx.Mediator);
        _diagnosticsFacade = new DiagnosticsFacade(_ctx.DiagnosticController, _ctx.Mediator);
        _capFacade = new CapabilitiesFacade(configProvider.Config);

        DevicePath = "SimulatorCashChanger";
    }

    // Lifecycle
    public override void Open()
    {
        _ctx.LifecycleManager.UpdateHandler(SkipStateVerification);
        _ctx.LifecycleManager.Open(base.Open);
    }
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
    public override void AdjustCashCounts(IEnumerable<CashCount> cashCounts)
    {
        ArgumentNullException.ThrowIfNull(cashCounts);
        _inventoryFacade.AdjustCashCounts(cashCounts, _configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode), _ctx.HardwareStatusManager);
    }
    public override CashCounts ReadCashCounts() => _inventoryFacade.ReadCashCounts(_configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode));

    /// <summary>現在の現金在庫数を文字列形式で調整します（UPOS 準拠用ヘルパー）。</summary>
    public void AdjustCashCounts(string cashCounts) => _inventoryFacade.AdjustCashCounts(cashCounts, _configManager.CurrencyCode, UposCurrencyHelper.GetCurrencyFactor(_configManager.CurrencyCode), _ctx.HardwareStatusManager);

    /// <summary>現在の現金在庫数を読み取り、文字列および不一致フラグとして返します（UPOS 準拠用ヘルパー）。</summary>
    public void ReadCashCounts(ref string cashCounts, ref bool discrepancy)
    {
        var result = ReadCashCounts();
        cashCounts = CashCountAdapter.FormatCashCounts(result.Counts);
        discrepancy = result.Discrepancy;
    }
    public virtual void PurgeCash() => _inventoryFacade.PurgeCash();

    // Diagnostics & Stats
    public override string CheckHealth(HealthCheckLevel level) => _checkHealthText = _diagnosticsFacade.CheckHealth(level);
    public override string CheckHealthText => _checkHealthText;
    public override string RetrieveStatistics(string[] statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        return _diagnosticsFacade.RetrieveStatistics(statistics);
    }
    public override void UpdateStatistics(Statistic[] statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        _diagnosticsFacade.UpdateStatistics(statistics);
    }
    public override void ResetStatistics(string[] statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        _diagnosticsFacade.ResetStatistics(statistics);
    }
    
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

    /// <summary>状態検証（Open, Claim 等のシーケンスチェック）をスキップするかどうかを取得または設定します。</summary>
    /// <remarks>シミュレータとしての利便性を優先する場合に true に設定します。</remarks>
    public bool SkipStateVerification
    {
        get => _ctx.Mediator.SkipStateVerification;
        set
        {
            if (_ctx.Mediator.SkipStateVerification == value) return;
            _ctx.Mediator.SkipStateVerification = value;
            _ctx.LifecycleManager.UpdateHandler(value);
        }
    }

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
    
    /// <summary>入金の要求額を取得または設定します。</summary>
    public decimal RequiredAmount { get => DepositController.RequiredAmount; set => DepositController.RequiredAmount = value; }

    /// <summary>現在入金処理中（計数中）かどうかを取得します。</summary>
    public bool IsDepositInProgress => DepositController.IsDepositInProgress;

    // DirectIO
    public override DirectIOData DirectIO(int command, int data, object obj)
    {
        _ctx.Mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
        var result = _directIOHandler.Handle(command, data, obj, this);
        _ctx.Mediator.SetSuccess();
        return result;
    }

    // Event Sink Implementation
    void IUposEventSink.NotifyEvent(EventArgs e)
    {
        if (_disposedValue) return;
        _eventNotifier.NotifyEvent(e);
    }

    void IUposEventSink.QueueEvent(EventArgs e)
    {
        if (_disposedValue) return;
        if (e is DataEventArgs de) base.QueueEvent(de);
        else if (e is StatusUpdateEventArgs se) base.QueueEvent(se);
        else if (e is DirectIOEventArgs die) base.QueueEvent(die);
    }

    void IUposEventSink.QueueDataEvent(DataEventArgs e) => base.QueueEvent(e);
    void IUposEventSink.QueueStatusUpdateEvent(StatusUpdateEventArgs se) => base.QueueEvent(se);

    protected virtual void NotifyEvent(EventArgs e)
    {
        if (_disposedValue) return;
        _eventNotifier?.NotifyEvent(e);
    }
    bool IUposEventSink.DataEventEnabled => DataEventEnabled;
    bool IUposEventSink.CapDepositDataEvent => CapDepositDataEvent;
    bool IUposEventSink.SkipStateVerification => SkipStateVerification;
    bool IUposEventSink.RealTimeDataEnabled => RealTimeDataEnabled;

    // Infrastructure
    public override string DeviceName => "SimulatorCashChanger";
    public override string DeviceDescription => "Virtual Cash Changer Simulator";

    // Internal Helpers (for Extensions)
    internal void SetAsyncProcessingInternal(bool isBusy) => _ctx.Mediator.IsBusy = isBusy;
    internal void FireEventInternal(EventArgs e) => NotifyEvent(e);

    // IDisposable
    private bool _disposedValue;

    protected override void Dispose(bool disposing)
    {
        if (_disposedValue) return;

        // [FIX] Set this flag IMMEDIATELY to prevent recursive calls during disposal/SDK-internal Close.
        _disposedValue = true;

        if (disposing)
        {
            try { _ctx?.Dispose(); } catch { /* Ignore cleanup errors */ }
        }

        try
        {
            // SDK の内部状態で既に Closed になっている場合に base.Dispose を呼ぶと例外が発生することがあるため、
            // 安全のために保護します。
            if (State != ControlState.Closed)
            {
                // [FIX] Explicitly reset RealTimeDataEnabled to stop internal event listeners 
                // before disposal to prevent NullReferenceException in POS SDK.
                // We wrap this and base.Dispose together as they are both interacting with the potentially unstable SDK state.
                if (CapRealTimeData)
                {
                    try { RealTimeDataEnabled = false; } catch { }
                }

                base.Dispose(disposing);
            }
        }
        catch (Exception ex)
        {
            // 終了処理中の例外はデバッグ出力に留め、アプリケーションの終了を妨げないようにします。
            System.Diagnostics.Debug.WriteLine($"[SimulatorCashChanger] Dispose SDK Error: {ex}");
        }
    }
    /// <inheritdoc/>
    public Observable<Unit> DepositChanged => DepositController.Changed;
}
