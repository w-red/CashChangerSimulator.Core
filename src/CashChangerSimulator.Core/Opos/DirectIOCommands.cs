namespace CashChangerSimulator.Core.Opos;

/// <summary>
/// SimulatorCashChanger で使用されるベンダー固有コマンド（DirectIO）の定数定義。
/// </summary>
public static class DirectIOCommands
{
    /// <summary>重複投入（Overlap）エラー状態を設定または解除します。 data: 1 (Error), 0 (Reset)</summary>
    public const int SET_OVERLAP = 10;

    /// <summary>メカニカルジャム（Jam）エラー状態を設定または解除します。 data: 1 (Error), 0 (Reset)</summary>
    public const int SET_JAM = 11;

    /// <summary>シミュレーターのバージョン情報を取得します。</summary>
    public const int GET_VERSION = 100;

    /// <summary>直前の入金セッションで投入された紙幣のシリアル番号一覧を取得します。</summary>
    public const int GET_DEPOSITED_SERIALS = 1002;

    /// <summary>[シミュレーション用] デバイスの取り外し(REMOVED)をエミュレートします。</summary>
    public const int SIMULATE_REMOVED = 20;

    /// <summary>[シミュレーション用] デバイスの装着(INSERTED)をエミュレートします。</summary>
    public const int SIMULATE_INSERTED = 21;
}
