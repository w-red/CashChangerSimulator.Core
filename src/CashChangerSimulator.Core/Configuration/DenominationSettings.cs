namespace CashChangerSimulator.Core.Configuration;

/// <summary>金種ごとの詳細設定を保持するデータモデル。</summary>
/// <remarks>
/// 表示名、初期枚数、各種センサー(NearEmpty/Full)のしきい値、
/// および釣銭(リサイクル)として使用可能かどうかのフラグを保持します。
/// </remarks>
public class DenominationSettings
{
    /// <summary>英語の表示名。</summary>
    public string? DisplayName { get; set; }

    /// <summary>日本語の表示名。</summary>
    public string? DisplayNameJP { get; set; }

    /// <summary>表示時のフォーマット指定子 (例: "N0", "N2")。</summary>
    public string? FormatSpecifier { get; set; }

    /// <summary>表示時の種別名称 (例: "Bill", "Coin", "Note")。</summary>
    public string? TypeName { get; set; }

    /// <summary>初期枚数。</summary>
    public int InitialCount { get; set; }

    /// <summary>ニアエンプティ判定値。</summary>
    public int NearEmpty { get; set; } = 5;

    /// <summary>ニアフル判定値。</summary>
    public int NearFull { get; set; } = 90;

    /// <summary>フル判定値。</summary>
    public int Full { get; set; } = 100;

    /// <summary>この金種を釣銭(リサイクル)として使用するかどうか。</summary>
    /// <remarks>false: 出金時の計算対象から除外されます。</remarks>
    public bool IsRecyclable { get; set; } = true;

    /// <summary>この金種を入金可能にするかどうか。</summary>
    /// <remarks>false: 入金時の計算対象から除外されます。</remarks>
    public bool IsDepositable { get; set; } = true;
}
