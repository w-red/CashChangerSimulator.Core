using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Services;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Models;

/// <summary>SimulatorCashChanger の内部コンポーネントを一括管理するコンテキストオブジェクト。</summary>
public sealed class SimulatorContext : IDisposable
{
    private bool _isDisposed;
    public required Inventory Inventory { get; init; }
    public required TransactionHistory History { get; init; }
    public required CashChangerManager Manager { get; init; }
    public required OverallStatusAggregator StatusAggregator { get; init; }
    public required HardwareStatusManager HardwareStatusManager { get; init; }
    public required DepositController DepositController { get; init; }
    public required DispenseController DispenseController { get; init; }
    public required DiagnosticController DiagnosticController { get; init; }
    public required UposMediator Mediator { get; init; }
    public required LifecycleManager LifecycleManager { get; init; }
    public required IUposEventNotifier EventNotifier { get; init; }
    public StatusCoordinator StatusCoordinator { get; internal set; } = null!;
    internal ConfigurationProvider? InternalConfigProvider { get; init; }

    /// <summary>依存関係からコンテキストを構築します（デフォルト値の解決を含む）。</summary>
    public static SimulatorContext Create(SimulatorDependencies deps, SimulatorCashChanger so, ILogger logger)
    {
        var hardwareStatusManager = deps.HardwareStatusManager ?? new HardwareStatusManager();
        var mediator = deps.Mediator as UposMediator ?? new UposMediator(so);

        ConfigurationProvider? internalConfigProvider = null;
        var configProvider = deps.ConfigProvider;
        if (configProvider == null)
        {
            configProvider = new ConfigurationProvider();
            internalConfigProvider = configProvider;
        }

        var inventory = deps.Inventory ?? new Inventory();
        var history = deps.History ?? new TransactionHistory();

        var lifecycleManager = new LifecycleManager(hardwareStatusManager, mediator, history, logger);
        var manager = deps.Manager ?? new CashChangerManager(inventory, history, new ChangeCalculator(), configProvider);

        var depositController = deps.DepositController ?? new DepositController(inventory, hardwareStatusManager, manager, configProvider);
        var dispenseController = deps.DispenseController ?? new DispenseController(manager, hardwareStatusManager, new HardwareSimulator(configProvider));

        var aggregator = deps.AggregatorProvider?.Aggregator ?? new OverallStatusAggregator(
            inventory.AllCounts
            .Select(kv => (kv.Key, Settings: configProvider.Config.GetDenominationSetting(kv.Key)))
            .Select(x => new CashStatusMonitor(inventory, x.Key, x.Settings.NearEmpty, x.Settings.NearFull, x.Settings.Full))
            .ToList());

        var diagnosticController = deps.DiagnosticController ?? new DiagnosticController(inventory, hardwareStatusManager);
        var eventNotifier = deps.EventNotifier ?? new UposEventNotifier(so);

        var ctx = new SimulatorContext
        {
            Inventory = inventory,
            History = history,
            Manager = manager,
            StatusAggregator = aggregator,
            HardwareStatusManager = hardwareStatusManager,
            DepositController = depositController,
            DispenseController = dispenseController,
            DiagnosticController = diagnosticController,
            Mediator = mediator,
            LifecycleManager = lifecycleManager,
            EventNotifier = eventNotifier,
            InternalConfigProvider = internalConfigProvider
        };

        ctx.StatusCoordinator = new StatusCoordinator(so, aggregator, hardwareStatusManager, depositController, dispenseController);

        return ctx;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed) return;

        StatusCoordinator?.Dispose();
        DepositController?.Dispose();
        DispenseController?.Dispose();
        StatusAggregator?.Dispose();
        HardwareStatusManager?.Dispose();
        InternalConfigProvider?.Dispose();

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
