using CashChangerSimulator.Device;

namespace CashChangerSimulator.Core.Exceptions;

/// <summary>デバイス操作に関するエラー情報を通知する例外クラス。</summary>
public class DeviceException : Exception
{
    /// <summary>空のインスタンスを初期化する。</summary>
    public DeviceException()
        : base("A device error has occurred.")
    {
        ErrorCode = DeviceErrorCode.Failure;
    }

    /// <summary>メッセージを指定して初期化する。</summary>
    /// <param name="message">例外メッセージ。</param>
    public DeviceException(string message)
        : base(message)
    {
        ErrorCode = DeviceErrorCode.Failure;
    }

    /// <summary>メッセージと内部例外を指定して初期化する。</summary>
    /// <param name="message">例外メッセージ。</param>
    /// <param name="innerException">内部例外。</param>
    public DeviceException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = DeviceErrorCode.Failure;
    }

    /// <summary>詳細なエラーコードを指定して初期化する。</summary>
    /// <param name="message">例外メッセージ。</param>
    /// <param name="errorCode">デバイスエラーコード。</param>
    /// <param name="errorCodeExtended">詳細エラーコード。</param>
    public DeviceException(string message, DeviceErrorCode errorCode, int errorCodeExtended = 0)
        : base(message)
    {
        ErrorCode = errorCode;
        ErrorCodeExtended = errorCodeExtended;
    }

    /// <summary>詳細なエラーコードと内部例外を指定して初期化する。</summary>
    /// <param name="message">例外メッセージ。</param>
    /// <param name="errorCode">デバイスエラーコード。</param>
    /// <param name="errorCodeExtended">詳細エラーコード。</param>
    /// <param name="inner">内部例外。</param>
    public DeviceException(string message, DeviceErrorCode errorCode, int errorCodeExtended, Exception inner)
        : base(message, inner)
    {
        ErrorCode = errorCode;
        ErrorCodeExtended = errorCodeExtended;
    }

    /// <summary>デバイスエラーコードを取得する。</summary>
    public DeviceErrorCode ErrorCode { get; }

    /// <summary>詳細エラーコードを取得する。</summary>
    public int ErrorCodeExtended { get; }
}
