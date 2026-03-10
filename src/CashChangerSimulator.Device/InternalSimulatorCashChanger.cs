using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using CashChangerSimulator.Device.Coordination;

namespace CashChangerSimulator.Device;

/// <summary>シミュレータ内部およびテストでのみ使用される機能を備えた <see cref="SimulatorCashChanger"/> の拡張クラス。</summary>
/// <remarks>本番用（サービスオブジェクト）のロジックと、シミュレータの利便性・テスト用フックを分離するために使用します。</remarks>
[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Internal Simulator Cash Changer", 1, 14)]
public class InternalSimulatorCashChanger : SimulatorCashChanger
{
    // Nullable に変更：LogProvider が null を返す可能性や、テスト環境での変動に対応
    private readonly ILogger<InternalSimulatorCashChanger>? _internalLogger;

    /// <summary>テスト用：構成を指定せずに初期化します。</summary>
    public InternalSimulatorCashChanger()
        : base(new SimulatorDependencies())
    {
        Context.Mediator.SkipStateVerification = true;
        _internalLogger = LogProvider.CreateLogger<InternalSimulatorCashChanger>();
    }

    /// <summary>テスト用のイベント通知アクション。UIのアクティビティフィードやテストでの検証に使用されます。</summary>
    public Action<EventArgs>? OnEventQueued;

    /// <summary>指定された引数で新しいインスタンスを初期化します。</summary>
    public InternalSimulatorCashChanger(SimulatorDependencies deps)
        : base(deps)
    {
        Context.Mediator.SkipStateVerification = true;
        _internalLogger = LogProvider.CreateLogger<InternalSimulatorCashChanger>();
    }

    /// <summary>テスト用：個別の依存関係を指定して初期化します（8個の引数版）。</summary>
    public InternalSimulatorCashChanger(
        ConfigurationProvider configProvider,
        Inventory inventory,
        TransactionHistory history,
        CashChangerManager manager,
        DepositController depositController,
        DispenseController dispenseController,
        OverallStatusAggregatorProvider aggregatorProvider,
        HardwareStatusManager hardwareStatusManager)
        : base(new SimulatorDependencies(
            configProvider,
            inventory,
            history,
            manager,
            depositController,
            dispenseController,
            aggregatorProvider,
            hardwareStatusManager))
    {
        Context.Mediator.SkipStateVerification = true;
        _internalLogger = LogProvider.CreateLogger<InternalSimulatorCashChanger>();
    }

    /// <summary>テスト用：最小限の依存関係で初期化します（named parameters対応）。</summary>
    public InternalSimulatorCashChanger(
        ConfigurationProvider? configProvider = null,
        Inventory? inventory = null,
        TransactionHistory? history = null,
        CashChangerManager? manager = null,
        DepositController? depositController = null,
        DispenseController? dispenseController = null,
        OverallStatusAggregatorProvider? aggregatorProvider = null,
        HardwareStatusManager? hardwareStatusManager = null,
        DiagnosticController? diagnosticController = null)
        : base(new SimulatorDependencies(
            configProvider,
            inventory,
            history,
            manager,
            depositController,
            dispenseController,
            aggregatorProvider,
            hardwareStatusManager,
            diagnosticController))
    {
        Context.Mediator.SkipStateVerification = true;
        _internalLogger = LogProvider.CreateLogger<InternalSimulatorCashChanger>();
    }

    /// <summary>イベント通知をオーバーライドし、OnEventQueued フックを実行します。</summary>
    protected override void NotifyEvent(System.EventArgs e)
    {
        OnEventQueued?.Invoke(e);
        base.NotifyEvent(e);
    }
}
