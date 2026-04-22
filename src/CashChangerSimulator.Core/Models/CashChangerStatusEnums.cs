namespace CashChangerSimulator.Core.Models;

/// <summary>驥｣驫ｭ讖溘・蝨ｨ蠎ｫ荳崎ｶｳ迥ｶ諷九ｒ陦ｨ縺吝・謖吝梛縲・/summary></summary>
public enum CashChangerStatus
{
    /// <summary>豁｣蟶ｸ縲・/summary></summary>
    OK = 0,

    /// <summary>遨ｺ縺ｮ迥ｶ諷九・/summary></summary>
    Empty = 11,

    /// <summary>遨ｺ縺ｫ霑代＞迥ｶ諷九・/summary></summary>
    NearEmpty = 12,
}

/// <summary>驥｣驫ｭ讖溘・貅譚ｯ迥ｶ諷九ｒ陦ｨ縺吝・謖吝梛縲・/summary></summary>
public enum CashChangerFullStatus
{
    /// <summary>豁｣蟶ｸ縲・/summary></summary>
    OK = 0,

    /// <summary>貅譚ｯ縺ｮ迥ｶ諷九・/summary></summary>
    Full = 21,

    /// <summary>貅譚ｯ縺ｫ霑代＞迥ｶ諷九・/summary></summary>
    NearFull = 22,
}
