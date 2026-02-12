using System.Collections.Generic;
using CsToml;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>
/// 釣銭機シミュレーターの設定を保持するクラス。
/// </summary>
[TomlSerializedObject]
public partial class SimulatorConfiguration
{
    [TomlValueOnSerialized]
    public InventorySettings Inventory { get; set; } = new();

    [TomlValueOnSerialized]
    public ThresholdSettings Thresholds { get; set; } = new();
}

[TomlSerializedObject]
public partial class InventorySettings
{
    [TomlValueOnSerialized]
    public Dictionary<string, int> InitialCounts { get; set; } = new()
    {
        { "10000", 10 },
        { "5000",  10 },
        { "2000",  0 },
        { "1000",  50 },
        { "500",   100 },
        { "100",   100 },
        { "50",    100 },
        { "10",    100 },
        { "5",     100 },
        { "1",     100 }
    };
}

[TomlSerializedObject]
public partial class ThresholdSettings
{
    [TomlValueOnSerialized]
    public int NearEmpty { get; set; } = 5;
    
    [TomlValueOnSerialized]
    public int NearFull { get; set; } = 90;
    
    [TomlValueOnSerialized]
    public int Full { get; set; } = 100;
}
