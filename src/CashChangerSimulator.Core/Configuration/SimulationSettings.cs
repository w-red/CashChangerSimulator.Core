using CsToml;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>シミュレーション動作の設定を保持するクラス。</summary>
[TomlSerializedObject]
public partial class SimulationSettings
{
    /// <summary>払い出し操作にかかる遅延時間（ミリ秒）。</summary>
    [TomlValueOnSerialized]
    public int DispenseDelayMs { get; set; } = 500;
}
