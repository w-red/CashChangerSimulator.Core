using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.PosForDotNet.Facades;
using CashChangerSimulator.Device.PosForDotNet.Models;
using CashChangerSimulator.Device.PosForDotNet.Services;
using Microsoft.Extensions.Logging;
using R3;

namespace CashChangerSimulator.Device.PosForDotNet;

/// <summary>
/// SimulatorCashChanger の核となるロジックを管理する内部クラス。
/// POS for .NET SDK からの依存を最小限に抑え、Facade やコントローラーのライフサイクルを管理します。
/// </summary>
internal sealed partial class UposCashChangerCore : IDisposable
{
    private readonly CompositeDisposable disposables = [];
    private readonly ILogger logger;

    public UposCashChangerCore(SimulatorDependencies deps, IUposEventSink eventSink)
    {
        ArgumentNullException.ThrowIfNull(deps);
        logger = LogProvider.CreateLogger<SimulatorCashChanger>();
        Context = SimulatorContext.Create(deps, (ICashChangerStatusSink)eventSink, logger);
        EventNotifier = (UposEventNotifier)Context.EventNotifier;
        Context.StatusCoordinator.Start();

        // ライフサイクルハンドラーを初期化
        Context.LifecycleManager.UpdateHandler(Context.Mediator.SkipStateVerification);

        var configProvider = deps.ConfigProvider ?? new ConfigurationProvider();
        ConfigManager = deps.ConfigurationManager ?? new UposConfigurationManager(configProvider, Context.Inventory, (IDeviceStateProvider)eventSink);
        ConfigManager.Initialize();

        DispenseFacade = new UposDispenseFacade(Context.DispenseController, Context.DepositController, Context.HardwareStatusManager, Context.Inventory, Context.Mediator);
        DepositFacade = new DepositFacade(Context.DepositController, Context.Mediator, Context.DiagnosticController);
        InventoryFacade = new InventoryFacade(Context.Inventory, Context.Manager, Context.Mediator);
        DiagnosticsFacade = new DiagnosticsFacade(Context.DiagnosticController, Context.Mediator);
        CapFacade = new CapabilitiesFacade(configProvider.Config);

        StateProperty = new ReactiveProperty<PosSharp.Abstractions.ControlState>(InternalStatusMonitor.MapToControlState(Context.LifecycleManager.State)).AddTo(disposables);
        StatusMonitor = new InternalStatusMonitor(Context);

        Context.HardwareStatusManager.IsConnected
            .Subscribe(v => LogHardwareConnection(v))
            .AddTo(disposables);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hardware connection: {IsConnected}")]
    private partial void LogHardwareConnection(bool isConnected);

    public SimulatorContext Context { get; }
    public UposEventNotifier EventNotifier { get; }
    public UposConfigurationManager ConfigManager { get; }
    public UposDispenseFacade DispenseFacade { get; }
    public DepositFacade DepositFacade { get; }
    public InventoryFacade InventoryFacade { get; }
    public DiagnosticsFacade DiagnosticsFacade { get; }
    public CapabilitiesFacade CapFacade { get; }
    public InternalStatusMonitor StatusMonitor { get; }
    public ReactiveProperty<PosSharp.Abstractions.ControlState> StateProperty { get; }

    public void Dispose()
    {
        disposables.Dispose();
        Context.Dispose();
    }
}
