namespace CashChangerSimulator.Core.Configuration;

/// <summary>金種ごとの詳細設定を保持するデータモデル。.</summary>
/// <remarks>
/// 表示名、初期枚数、各種センサー（NearEmpty/Full）のしきい値、
/// および釣銭（リサイクル）として使用可能かどうかのフラグを保持します。.
/// </remarks>
public class DenominationSettings
{
    /// <summary>Gets or sets 英語の表示名。.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets 日本語の表示名。.</summary>
    public string? DisplayNameJP { get; set; }

    /// <summary>Gets or sets 初期枚数。.</summary>
    public int InitialCount { get; set; }

    /// <summary>Gets or sets nearEmpty 判定値。.</summary>
    public int NearEmpty { get; set; } = 5;

    /// <summary>Gets or sets nearFull 判定値。.</summary>
    public int NearFull { get; set; } = 90;

    /// <summary>Gets or sets full 判定値。.</summary>
    public int Full { get; set; } = 100;

    /// <summary>Gets or sets a value indicating whether この金種を釣銭（リサイクル）として使用するかどうか。false の場合、出金時の計算対象から除外されます。.</summary>
    public bool IsRecyclable { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether この金種を入金可能にするかどうか。false の場合、入金処理時にこの金種は受け付けられません。.</summary>
    public bool IsDepositable { get; set; } = true;
}
