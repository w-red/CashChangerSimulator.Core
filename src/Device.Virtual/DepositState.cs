using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>入金セッションの内部状態を保持するクラス。DepositController からの責務分離のために導入されました。</summary>
internal sealed class DepositState
{
    /// <summary>投入された合計金額。</summary>
    public decimal DepositAmount { get; set; }

    /// <summary>オーバーフローした金額。</summary>
    public decimal OverflowAmount { get; set; }

    /// <summary>リジェクトされた金額。</summary>
    public decimal RejectAmount { get; set; }

    /// <summary>必要入金金額。</summary>
    public decimal RequiredAmount { get; set; }

    /// <summary>現在の預入状態。</summary>
    public DeviceDepositStatus Status { get; set; } = DeviceDepositStatus.None;

    /// <summary>一時停止中かどうか。</summary>
    public bool IsPaused { get; set; }

    /// <summary>確定済みかどうか。</summary>
    public bool IsFixed { get; set; }

    /// <summary>デバイスがビジー状態かどうか。</summary>
    public bool IsBusy { get; set; }

    /// <summary>直近のエラーコード。</summary>
    public DeviceErrorCode LastErrorCode { get; set; } = DeviceErrorCode.Success;

    /// <summary>直近の拡張エラーコード。</summary>
    public int LastErrorCodeExtended { get; set; }

    /// <summary>投入された金種ごとの枚数。</summary>
    public Dictionary<DenominationKey, int> Counts { get; } = [];

    /// <summary>投入された紙幣のシリアル番号リスト。</summary>
    public List<string> DepositedSerials { get; } = [];

    /// <summary>確定時に同期される直前のシリアル番号リスト。</summary>
    public List<string> LastDepositedSerials { get; } = [];

    /// <summary>セッション状態を初期化します。</summary>
    public void Reset()
    {
        DepositAmount = 0m;
        OverflowAmount = 0m;
        RejectAmount = 0m;
        Counts.Clear();
        DepositedSerials.Clear();
        Status = DeviceDepositStatus.None;
        IsPaused = false;
        IsFixed = false;
        LastErrorCode = DeviceErrorCode.Success;
        LastErrorCodeExtended = 0;
    }
}
