namespace CashChangerSimulator.Core.Opos;

/// <summary>
/// SimulatorCashChanger で使用されるベンダー固有コマンド（DirectIO）の定数定義。
/// </summary>
public static class DirectIOCommands
{
    /// <summary>重複投入（Overlap）エラー状態を設定または解除します。 data: 1 (Error), 0 (Reset)</summary>
    public const int SetOverlap = 10;

    /// <summary>メカニカルジャム（Jam）エラー状態を設定または解除します。 data: 1 (Error), 0 (Reset)</summary>
    public const int SetJam = 11;

    /// <summary>不一致(Discrepancy)が発生した状態を強制設定します。 data: 1 (Error), 0 (Reset)</summary>
    public const int SetDiscrepancy = 12;

    /// <summary>[シミュレーション用] デバイスの取り外し(REMOVED)をエミュレートします。</summary>
    public const int SimulateRemoved = 20;

    /// <summary>[シミュレーション用] デバイスの装着(INSERTED)をエミュレートします。</summary>
    public const int SimulateInserted = 21;

    /// <summary>シミュレーターのバージョン情報を取得します。</summary>
    public const int GetVersion = 100;

    /// <summary>現在発生中のジャム箇所を取得します。 obj: 文字列で箇所を返却します。</summary>
    public const int GetJamLocation = 111;

    /// <summary>現金を指定の文字列（UPOS形式）で調整します。 obj: "1000:10,500:5" 等の文字列。</summary>
    public const int AdjustCashCountsStr = 101;

    /// <summary>直前の入金セッションで投入された紙幣のシリアル番号一覧を取得します。</summary>
    public const int GetDepositedSerials = 1002;
}
