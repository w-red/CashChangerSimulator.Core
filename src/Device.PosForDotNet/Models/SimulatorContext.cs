using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.PosForDotNet.Services;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.PosForDotNet.Models;

/// <summary>シミュレータの構成要素（マネージャー、コントローラー、各種ステータスなど）を集約して管理するコンテキストクラス。</summary>
/// <remarks>
/// 実行時に必要なオブジェクトを一元管理し、<see cref="SimulatorCashChanger"/> 内部でのデータ共有やイベント通知を円滑にします。
/// </remarks>
public class SimulatorContext : IDisposable
{
    public ConfigurationProvider ConfigProvider { get; }
    public Inventory Inventory { get; }
    public TransactionHistory History { get; }
    public CashChangerManager Manager { get; }
    public DepositController DepositController { get; }
    public DispenseController DispenseController { get; }
    public OverallStatusAggregator Aggregator { get; }
    public HardwareStatusManager HardwareStatusManager { get; }
    public DiagnosticController DiagnosticController { get; }
    public IUposMediator Mediator { get; }
    public LifecycleManager LifecycleManager { get; }
    public StatusCoordinator StatusCoordinator { get; }
    public IUposEventNotifier EventNotifier { get; }
    public MonitorsProvider MonitorsProvider { get; }

    private bool _disposed;

    private SimulatorContext(SimulatorDependencies deps, ICashChangerStatusSink sink, ILogger logger)
    {
        ConfigProvider = deps.ConfigProvider ?? new ConfigurationProvider();
        Inventory = deps.Inventory ?? new Inventory();
        History = deps.History ?? new TransactionHistory();
        HardwareStatusManager = deps.HardwareStatusManager ?? new HardwareStatusManager();
        DiagnosticController = deps.DiagnosticController ?? new DiagnosticController(Inventory, HardwareStatusManager);

        var metadataProvider = new CurrencyMetadataProvider(ConfigProvider);
        MonitorsProvider = new MonitorsProvider(Inventory, ConfigProvider, metadataProvider);

        // Use the monitors from the MonitorsProvider for aggregation
        Aggregator = new OverallStatusAggregator(MonitorsProvider.Monitors);

        var calculator = new ChangeCalculator();
        Manager = deps.Manager ?? new CashChangerManager(Inventory, History, calculator, ConfigProvider);
        DepositController = deps.DepositController ?? new DepositController(Inventory, HardwareStatusManager, Manager, ConfigProvider);
        DispenseController = deps.DispenseController ?? new DispenseController(Manager, HardwareStatusManager, new HardwareSimulator(ConfigProvider));

        Mediator = deps.Mediator ?? new UposMediator();
        EventNotifier = deps.EventNotifier ?? new UposEventNotifier();

        LifecycleManager = new LifecycleManager(HardwareStatusManager, Mediator, History, logger);
        StatusCoordinator = new StatusCoordinator(sink, Aggregator, HardwareStatusManager, DepositController, DispenseController);

        // 排他制御の設定
        if (!string.IsNullOrEmpty(deps.GlobalLockFilePath))
        {
            var lockManager = new GlobalLockManager(deps.GlobalLockFilePath, logger);
            HardwareStatusManager.SetGlobalLockManager(lockManager);
        }
    }

    /// <summary>シミュレータの依存関係を解決してコンテキストを生成します。</summary>
    public static SimulatorContext Create(SimulatorDependencies deps, ICashChangerStatusSink sink, ILogger logger)
    {
        var ctx = new SimulatorContext(deps, sink, logger);
        ctx.Mediator.Initialize(sink, logger, ctx.StatusCoordinator, ctx.HardwareStatusManager);
        if (sink is IUposEventSink eventSink)
        {
            ctx.EventNotifier.Initialize(eventSink);
        }
        return ctx;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        DepositController?.Dispose();
        DispenseController?.Dispose();
        DiagnosticController?.Dispose();
        StatusCoordinator?.Dispose();
        HardwareStatusManager?.Dispose();
        ConfigProvider?.Dispose();
        MonitorsProvider?.Dispose();
        Aggregator?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
