namespace CashChangerSimulator.Core.Models;

/// <summary>
/// Specifies the response to a device error.
/// デバイスエラーに対する応答を指定します。
/// </summary>
public enum DeviceErrorResponse
{
    /// <summary>
    /// None or unknown response.
    /// なし、または不明な応答。
    /// </summary>
    None = 0,

    /// <summary>
    /// Retry the asynchronous operation.
    /// 非同期操作を再試行します。
    /// </summary>
    Retry = 1,

    /// <summary>
    /// Clear the asynchronous operation and any related buffered data.
    /// 非同期操作と関連するバッファリングされたデータをクリアします。
    /// </summary>
    Clear = 2,

    /// <summary>
    /// Continue processing the asynchronous operation.
    /// 非同期操作の処理を続行します。
    /// </summary>
    Continue = 3,
}
