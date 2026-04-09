namespace CashChangerSimulator.Core.Models;

/// <summary>デバイスエラーに対する応答を指定します。</summary>
public enum DeviceErrorResponse
{
    /// <summary>なし、または不明な応答。</summary>
    None = 0,

    /// <summary>非同期操作を再試行します。</summary>
    Retry = 1,

    /// <summary>非同期操作と関連するバッファリングされたデータをクリアします。</summary>
    Clear = 2,

    /// <summary>非同期操作の処理を続行します。</summary>
    Continue = 3,
}
