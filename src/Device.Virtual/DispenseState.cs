using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>出金セッションの内部状態を保持する不変レコード。DispenseController からの責務分離のために導入されました。</summary>
public sealed record DispenseState(
    CashDispenseStatus Status = CashDispenseStatus.Idle,
    DeviceErrorCode LastErrorCode = DeviceErrorCode.Success,
    int LastErrorCodeExtended = 0)
{
    /// <summary>処理中かどうかを取得します。</summary>
    public bool IsBusy => Status == CashDispenseStatus.Busy;
}
