namespace CashChangerSimulator.Core.Configuration;

/// <summary>金種ごとの詳細設定を保持するクラス。</summary>
public class DenominationSettings
{
    /// <summary>ユーザーが設定した表示名。</summary>
    public string? DisplayName { get; set; }

    /// <summary>初期枚数。</summary>
    public int InitialCount { get; set; }

    /// <summary>NearEmpty 判定値。</summary>
    public int NearEmpty { get; set; } = 5;

    /// <summary>NearFull 判定値。</summary>
    public int NearFull { get; set; } = 90;

    /// <summary>Full 判定値。</summary>
    public int Full { get; set; } = 100;
}
