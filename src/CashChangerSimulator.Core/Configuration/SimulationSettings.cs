using CsToml;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>シミュレーターのUIモード</summary>
public enum UIMode
{
    /// <summary>標準モード。</summary>
    Standard,
    /// <summary>POS取引モード。</summary>
    PosTransaction
}

/// <summary>シミュレーション挙動の設定クラス。</summary>
[TomlSerializedObject]
public partial class SimulationSettings
{
    /// <summary>UIの動作モード。</summary>
    [TomlValueOnSerialized]
    public UIMode UIMode { get; set; } = UIMode.Standard;
}
