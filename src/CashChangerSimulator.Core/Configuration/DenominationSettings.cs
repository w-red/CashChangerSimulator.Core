namespace CashChangerSimulator.Core.Configuration;

/// <summary>金種ごとの詳細設定を保持するデータモデル。</summary>
/// <remarks>
/// 表示名、初期枚数、各種センサー（NearEmpty/Full）のしきい値、
/// および釣銭（リサイクル）として使用可能かどうかのフラグを保持します。
/// </remarks>
public class DenominationSettings
{
    /// <summary>英語の表示名。</summary>
    public string? DisplayName { get; set; }

    /// <summary>日本語の表示名。</summary>
    public string? DisplayNameJP { get; set; }

    /// <summary>初期枚数。</summary>
    public int InitialCount { get; set; }

    /// <summary>NearEmpty 判定値。</summary>
    public int NearEmpty { get; set; } = 5;

    /// <summary>NearFull 判定値。</summary>
    public int NearFull { get; set; } = 90;

    /// <summary>Full 判定値。</summary>
    public int Full { get; set; } = 100;

    /// <summary>この金種を釣銭（リサイクル）として使用するかどうか。false の場合、出金時の計算対象から除外されます。</summary>
    public bool IsRecyclable { get; set; } = true;
}
