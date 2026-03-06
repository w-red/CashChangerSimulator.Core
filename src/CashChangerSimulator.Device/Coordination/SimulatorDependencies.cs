using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Services;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>
/// SimulatorCashChanger の初期化に必要な依存関係をまとめる Parameter Object。
/// </summary>
public record SimulatorDependencies(
    ConfigurationProvider? ConfigProvider = null,
    Inventory? Inventory = null,
    TransactionHistory? History = null,
    CashChangerManager? Manager = null,
    DepositController? DepositController = null,
    DispenseController? DispenseController = null,
    OverallStatusAggregatorProvider? AggregatorProvider = null,
    HardwareStatusManager? HardwareStatusManager = null,
    DiagnosticController? DiagnosticController = null,
    IUposMediator? Mediator = null,
    IUposConfigurationManager? ConfigurationManager = null,
    IUposEventNotifier? EventNotifier = null
);
