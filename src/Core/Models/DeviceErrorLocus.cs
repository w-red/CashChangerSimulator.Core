namespace CashChangerSimulator.Core.Models;

/// <summary>
/// Specifies the location where a device error occurred.
/// デバイスエラーが発生した場所を指定します。
/// </summary>
public enum DeviceErrorLocus
{
    /// <summary>
    /// None or unknown locus.
    /// なし、または不明な発生場所。
    /// </summary>
    None = 0,

    /// <summary>
    /// An error occurred during an asynchronous output operation.
    /// 非同期出力操作中にエラーが発生しました。
    /// </summary>
    Output = 1,

    /// <summary>
    /// An error occurred during an asynchronous input operation.
    /// 非同期入力操作中にエラーが発生しました。
    /// </summary>
    Input = 2,

    /// <summary>
    /// An error occurred during an asynchronous input operation, and one or more input messages are available.
    /// 非同期入力操作中にエラーが発生し、1つ以上の入力メッセージが利用可能です。
    /// </summary>
    InputData = 3,
}
