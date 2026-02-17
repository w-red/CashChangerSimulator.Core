using CsToml;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>
/// 金種ごとの詳細設定を保持するクラス。
/// </summary>
[TomlSerializedObject]
public partial class DenominationSettings
{
    /// <summary>ユーザーが設定した表示名。</summary>
    [TomlValueOnSerialized]
    public string? DisplayName { get; set; }

    /// <summary>初期枚数。</summary>
    [TomlValueOnSerialized]
    public int InitialCount { get; set; }

    /// <summary>NearEmpty 判定値。</summary>
    [TomlValueOnSerialized]
    public int NearEmpty { get; set; } = 5;

    /// <summary>NearFull 判定値。</summary>
    [TomlValueOnSerialized]
    public int NearFull { get; set; } = 90;

    /// <summary>Full 判定値。</summary>
    [TomlValueOnSerialized]
    public int Full { get; set; } = 100;
}
