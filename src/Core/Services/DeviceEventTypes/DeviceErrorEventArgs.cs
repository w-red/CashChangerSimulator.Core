using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>
/// Event arguments for error reporting events.
/// エラー報告イベント用のイベント引数。
/// </summary>
public class DeviceErrorEventArgs : DeviceEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceErrorEventArgs"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorCodeExtended">The extended error code.</param>
    /// <param name="errorLocus">The locus where the error occurred.</param>
    /// <param name="errorResponse">The initial error response.</param>
    public DeviceErrorEventArgs(
        DeviceErrorCode errorCode,
        int errorCodeExtended,
        DeviceErrorLocus errorLocus,
        DeviceErrorResponse errorResponse)
    {
        ErrorCode = errorCode;
        ErrorCodeExtended = errorCodeExtended;
        ErrorLocus = errorLocus;
        ErrorResponse = errorResponse;
    }

    /// <summary>
    /// Gets the error code.
    /// エラーコードを取得します。
    /// </summary>
    public DeviceErrorCode ErrorCode { get; }

    /// <summary>
    /// Gets the extended error code.
    /// 拡張エラーコードを取得します。
    /// </summary>
    public int ErrorCodeExtended { get; }

    /// <summary>
    /// Gets the locus where the error occurred.
    /// エラーが発生した場所を取得します。
    /// </summary>
    public DeviceErrorLocus ErrorLocus { get; }

    /// <summary>
    /// Gets or sets the response to the error.
    /// エラーに対する応答を取得または設定します。
    /// </summary>
    public DeviceErrorResponse ErrorResponse { get; set; }
}
