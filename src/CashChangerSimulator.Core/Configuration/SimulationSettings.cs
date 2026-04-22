namespace CashChangerSimulator.Core.Configuration;

/// <summary>シミュレーション動作の設定を保持するクラス。</summary>
public class SimulationSettings
{
    /// <summary>シミュレーション設定の新しいインスタンスを初期化します。</summary>
    public SimulationSettings()
    {
    }

    private int dispenseDelayMs = 500;
    private int depositDelayMs = 500;

    /// <summary>払い出し操作にかかる遅延時間(ミリ秒)。</summary>
    public int DispenseDelayMs
    {
        get => dispenseDelayMs;
        set => dispenseDelayMs = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "DispenseDelayMs cannot be negative.");
    }

    /// <summary>入金確定操作にかかる遅延時間(ミリ秒)。</summary>
    public int DepositDelayMs
    {
        get => depositDelayMs;
        set => depositDelayMs = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "DepositDelayMs cannot be negative.");
    }

    /// <summary>起動時にデバイスを自動オープン (Hot Start) するかどうか。</summary>
    public bool HotStart { get; set; }

    /// <summary>リアルタイムデータの通知能力を持っているかどうか。</summary>
    public bool CapRealTimeData { get; set; } = true;
}
