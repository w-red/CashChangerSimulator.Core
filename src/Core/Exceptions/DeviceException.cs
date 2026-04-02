using CashChangerSimulator.Device;

namespace CashChangerSimulator.Core.Exceptions;

/// <summary>デバイス操作に関するエラー情報を通知する例外クラス。</summary>
public class DeviceException : Exception
{
    public DeviceErrorCode ErrorCode { get; }
    public int ErrorCodeExtended { get; }

    public DeviceException(string message, DeviceErrorCode errorCode = DeviceErrorCode.Failure, int errorCodeExtended = 0)
        : base(message)
    {
        ErrorCode = errorCode;
        ErrorCodeExtended = errorCodeExtended;
    }
}
