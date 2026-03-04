using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using System.Reflection;

namespace CashChangerSimulator.Device;

/// <summary>シミュレータ内部およびテストでのみ使用される機能を備えた <see cref="SimulatorCashChanger"/> の拡張クラス。</summary>
/// <remarks>本番用（サービスオブジェクト）のロジックと、シミュレータの利便性・テスト用フックを分離するために使用します。</remarks>
[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Internal Simulator Cash Changer", 1, 14)]
public class InternalSimulatorCashChanger : SimulatorCashChanger
{
    private readonly ILogger<InternalSimulatorCashChanger> _internalLogger;

    /// <summary>テスト用：構成を指定せずに初期化します。</summary>
    public InternalSimulatorCashChanger()
        : base(null, null, null, null, null, null, null, null)
    {
        _internalLogger = LogProvider.CreateLogger<InternalSimulatorCashChanger>();
    }

    /// <summary>シミュレータUIからの Open 呼び出しを模擬します。</summary>
    public override void Open()
    {
        if (!SkipStateVerification)
        {
            base.Open();
            return;
        }

        // Simulate hardware connection for tests
        var field = typeof(SimulatorCashChanger).GetField("_hardwareStatusManager", BindingFlags.Instance | BindingFlags.NonPublic);
        var hardware = field?.GetValue(this) as HardwareStatusManager;
        hardware?.SetConnected(true);
    }

    /// <summary>シミュレータUIからの Close 呼び出しを模擬します。</summary>
    public new void Close() => base.Close();

    /// <summary>シミュレータUIからの Claim 呼び出しを模擬します。</summary>
    public override void Claim(int timeout)
    {
        if (SkipStateVerification) return;
        base.Claim(timeout);
    }

    /// <summary>シミュレータUIからの Release 呼び出しを模擬します。</summary>
    public new void Release() => base.Release();

    /// <summary>テスト用: VerifyState チェックをスキップ。OPOS ライフサイクルが利用できない単体テスト環境で使用。</summary>
    public override bool SkipStateVerification { get; set; }

    /// <summary>テスト用のイベント通知アクション。UIのアクティビティフィードやテストでの検証に使用されます。</summary>
    public Action<EventArgs>? OnEventQueued;

    /// <summary>指定された引数で新しいインスタンスを初期化します。</summary>
    public InternalSimulatorCashChanger(
        ConfigurationProvider? configProvider = null,
        Inventory? inventory = null,
        TransactionHistory? history = null,
        CashChangerManager? manager = null,
        DepositController? depositController = null,
        DispenseController? dispenseController = null,
        OverallStatusAggregatorProvider? aggregatorProvider = null,
        HardwareStatusManager? hardwareStatusManager = null)
        : base(configProvider, inventory, history, manager, depositController, dispenseController, aggregatorProvider, hardwareStatusManager)
    {
        _internalLogger = LogProvider.CreateLogger<InternalSimulatorCashChanger>();
    }

    /// <summary>イベント通知をオーバーライドし、OnEventQueued フックを実行します。</summary>
    protected override void NotifyEvent(EventArgs e)
    {
        OnEventQueued?.Invoke(e);
        
        if (SkipStateVerification) return;
        base.NotifyEvent(e);
    }
}
