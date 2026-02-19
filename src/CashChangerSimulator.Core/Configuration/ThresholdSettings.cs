using CsToml;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>ステータス判定用のしきい値を管理する設定クラス。</summary>
[TomlSerializedObject]
public partial class ThresholdSettings
{
    /// <summary>NearEmpty と判定する枚数（この枚数以下の場合）。</summary>
    [TomlValueOnSerialized]
    public int NearEmpty { get; set; } = 5;

    /// <summary>NearFull と判定する枚数（この枚数以上の場合）。</summary>
    [TomlValueOnSerialized]
    public int NearFull { get; set; } = 90;

    /// <summary>Full と判定する枚数（この枚数以上の場合）。</summary>
    [TomlValueOnSerialized]
    public int Full { get; set; } = 100;
}
