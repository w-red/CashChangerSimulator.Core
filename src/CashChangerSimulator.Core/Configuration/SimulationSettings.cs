namespace CashChangerSimulator.Core.Configuration;

/// <summary>シミュレーション動作の設定を保持するクラス。</summary>
public class SimulationSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationSettings"/> class.
    /// </summary>
    public SimulationSettings()
    {
        DispenseDelayMs = 500;
    }

    /// <summary>払い出し操作にかかる遅延時間（ミリ秒）。</summary>
    public int DispenseDelayMs
    {
        get => field;
        set => field = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                "DispenseDelayMs cannot be negative.");
    }

    /// <summary>起動時にデバイスを自動オープン (Hot Start) するかどうか。</summary>
    public bool HotStart { get; set; }

    /// <summary>リアルタイムデータの通知能力を持っているかどうか。</summary>
    public bool CapRealTimeData { get; set; } = true;
}
