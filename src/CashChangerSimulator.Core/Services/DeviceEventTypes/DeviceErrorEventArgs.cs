using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>エラー報告イベント用のイベント引数。</summary>
public class DeviceErrorEventArgs : DeviceEventArgs
{
    /// <summary>エラー情報を指定してインスタンスを初期化します。</summary>
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

    /// <summary>エラーコードを取得します。</summary>
    public DeviceErrorCode ErrorCode { get; }

    /// <summary>拡張エラーコードを取得します。</summary>
    public int ErrorCodeExtended { get; }

    /// <summary>エラーが発生した場所を取得します。</summary>
    public DeviceErrorLocus ErrorLocus { get; }

    /// <summary>エラーに対する応答を取得または設定します。</summary>
    public DeviceErrorResponse ErrorResponse { get; set; }
}
