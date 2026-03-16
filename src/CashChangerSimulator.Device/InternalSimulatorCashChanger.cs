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
public class InternalSimulatorCashChanger : SimulatorCashChanger, IDeviceSimulator
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

    /// <summary>テスト用：Open 時に例外をシミュレートするかどうかを制御します。</summary>
    public bool SimulateOpenException { get; set; }

    /// <summary>テスト用：Close 時に例外をシミュレートするかどうかを制御します。</summary>
    public bool SimulateCloseException { get; set; }

    /// <summary>テスト用：出金時に例外をシミュレートするかどうかを制御します。</summary>
    public bool SimulateDispenseException { get; set; }

    /// <summary>テスト用：OPOS コールの履歴を保持します。</summary>
    public List<string> OposHistory { get; } = [];

    /// <inheritdoc/>
    public override void Open()
    {
        OposHistory.Add("Open");
        if (SimulateOpenException) throw new System.IO.IOException("Simulated open exception");
        base.Open();
    }

    /// <inheritdoc/>
    public override void Close()
    {
        OposHistory.Add("Close");
        if (SimulateCloseException) throw new System.IO.IOException("Simulated close exception");
        base.Close();
    }

    /// <inheritdoc/>
    public override void DispenseCash(CashCount[] cashCounts)
    {
        OposHistory.Add($"DispenseCash: {string.Join(",", cashCounts.Select(c => c.NominalValue.ToString()))}");
        if (SimulateDispenseException) throw new PosControlException("Simulated dispense exception", ErrorCode.Failure);
        base.DispenseCash(cashCounts);
    }

    /// <summary>IDeviceSimulator の実装として、払い出し動作のシミュレーションを行います。</summary>
    public async Task SimulateDispenseAsync(System.Threading.CancellationToken ct = default)
    {
        // ViewModels から呼び出された際の履歴を記録（テスト検証用）
        OposHistory.Add("DispenseCash (Triggered)");
        
        if (SimulateDispenseException)
        {
            throw new PosControlException("Simulated dispense exception", ErrorCode.Failure);
        }
        await Task.Delay(10, ct);
    }

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
