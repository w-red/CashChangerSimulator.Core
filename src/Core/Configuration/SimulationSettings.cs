namespace CashChangerSimulator.Core.Configuration;

/// <summary>シミュレーション動作の設定を保持するクラス。</summary>
public class SimulationSettings
{
    private int dispenseDelayMs = 500;

    /// <summary>Gets or sets 払い出し操作にかかる遅延時間（ミリ秒）。</summary>
    public int DispenseDelayMs
    {
        get => dispenseDelayMs;
        set => dispenseDelayMs = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                "DispenseDelayMs cannot be negative.");
    }

    /// <summary>Gets or sets a value indicating whether 起動時にデバイスを自動オープン (Hot Start) するかどうか。</summary>
    public bool HotStart { get; set; }

    /// <summary>Gets or sets a value indicating whether リアルタイムデータの通知能力を持っているかどうか。</summary>
    public bool CapRealTimeData { get; set; } = true;
}
