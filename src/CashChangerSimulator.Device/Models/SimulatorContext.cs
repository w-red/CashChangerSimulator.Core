using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Facades;

namespace CashChangerSimulator.Device.Models;

/// <summary>
/// SimulatorCashChanger の内部コンポーネントを一括管理するコンテキストオブジェクト。
/// </summary>
public class SimulatorContext
{
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
    public StatusCoordinator StatusCoordinator { get; internal set; } = null!;
}
