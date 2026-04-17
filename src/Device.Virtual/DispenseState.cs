using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>出金セッションの内部状態を保持するクラス。DispenseController からの責務分離のために導入されました。</summary>
internal sealed class DispenseState
{
    /// <summary>現在のステータスを取得または設定します。</summary>
    public CashDispenseStatus Status { get; set; } = CashDispenseStatus.Idle;

    /// <summary>最後に発生したエラーコードを取得または設定します。</summary>
    public DeviceErrorCode LastErrorCode { get; set; } = DeviceErrorCode.Success;

    /// <summary>最後に発生した詳細エラーコードを取得または設定します。</summary>
    public int LastErrorCodeExtended { get; set; }

    /// <summary>処理中かどうかを取得します。</summary>
    public bool IsBusy => Status == CashDispenseStatus.Busy;

    /// <summary>セッション状態をリセットします。</summary>
    public void Reset()
    {
        Status = CashDispenseStatus.Idle;
        LastErrorCode = DeviceErrorCode.Success;
        LastErrorCodeExtended = 0;
    }
}
