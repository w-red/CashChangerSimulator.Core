using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.Device;

/// <summary>現金入出金機の基本操作と状態監視を定義する抽象インターフェース。</summary>
public interface ICashChangerDevice : IDisposable
{
    // Lifecycle
    Task OpenAsync();
    Task CloseAsync();

    // Operations
    Task BeginDepositAsync();
    Task FixDepositAsync();
    Task EndDepositAsync(DepositAction action);
    Task DispenseAsync(int amount);
    Task DispenseAsync(IEnumerable<CashDenominationCount> counts);
    
    // Inventory
    Task<Inventory> ReadInventoryAsync();
    Task AdjustInventoryAsync(IEnumerable<CashDenominationCount> counts);

    // Diagnostics
    Task<string> CheckHealthAsync();

    // Observable States
    ReadOnlyReactiveProperty<bool> IsBusy { get; }
    ReadOnlyReactiveProperty<DeviceControlState> State { get; }
}

/// <summary>デバイスの制御状態。</summary>
public enum DeviceControlState
{
    Closed,
    Idle,
    Busy,
    Error
}

/// <summary>入金状態。</summary>
public enum DeviceDepositStatus
{
    None,
    Start,
    Counting,
    End
}

/// <summary>入金一時停止制御。</summary>
public enum DeviceDepositPause
{
    Pause,
    Resume
}

/// <summary>入金確定時のアクション。</summary>
public enum DepositAction
{
    Store,
    Repay
}

/// <summary>特定の金種とその数量を保持します。</summary>
public record CashDenominationCount(decimal Denomination, int Count);

/// <summary>デバイス操作の結果コード。</summary>
/// <remarks>POS for .NET の ErrorCode とマッピング可能な、プラットフォーム非依存のコードを定義します。</remarks>
public enum DeviceErrorCode
{
    Success = 0,
    Failure = 111,
    Busy = 112,
    Timeout = 113,
    NoInventory = 114,
    Illegal = 115,
    Overlapped = 116,
    Jammed = 117,
    Extended = 200 // Extended codes
}

/// <summary>デバイス操作に関する例外。</summary>
public class DeviceException(string message, DeviceErrorCode errorCode = DeviceErrorCode.Failure, int errorCodeExtended = 0) : Exception(message)
{
    public DeviceErrorCode ErrorCode { get; } = errorCode;
    public int ErrorCodeExtended { get; } = errorCodeExtended;
}

/// <summary>ヘルスチェックのレベル。</summary>
public enum DeviceHealthCheckLevel
{
    Internal,
    External,
    Interactive
}
