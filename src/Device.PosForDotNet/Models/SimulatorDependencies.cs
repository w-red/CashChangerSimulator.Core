using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.PosForDotNet.Services;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Models;

/// <summary>シミュレータの構築に必要な依存オブジェクトを保持するレコード。.</summary>
/// <remarks>テストや DI コンテナからの注入を容易にするために使用します。.</remarks>
public record SimulatorDependencies(
    ConfigurationProvider? ConfigProvider = null,
    Inventory? Inventory = null,
    TransactionHistory? History = null,
    CashChangerManager? Manager = null,
    DepositController? DepositController = null,
    DispenseController? DispenseController = null,
    OverallStatusAggregatorProvider? AggregatorProvider = null, // Backward matching for legacy tests
    HardwareStatusManager? HardwareStatusManager = null,
    DiagnosticController? DiagnosticController = null,
    IUposMediator? Mediator = null,
    IUposEventNotifier? EventNotifier = null,
    UposConfigurationManager? ConfigurationManager = null,
    string? GlobalLockFilePath = null);
