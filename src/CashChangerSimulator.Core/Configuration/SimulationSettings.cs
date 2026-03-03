namespace CashChangerSimulator.Core.Configuration;

/// <summary>シミュレーション動作の設定を保持するクラス。</summary>
public class SimulationSettings
{
    private int _dispenseDelayMs = 500;

    /// <summary>払い出し操作にかかる遅延時間（ミリ秒）。</summary>
    public int DispenseDelayMs
    {
        get => _dispenseDelayMs;
        set => _dispenseDelayMs = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "DispenseDelayMs cannot be negative.");
    }

    /// <summary>起動時にデバイスが Closed 状態から開始するかどうか。</summary>
    public bool ColdStart { get; set; } = false;
}
