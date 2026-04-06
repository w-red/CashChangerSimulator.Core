using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>カスタム例外クラスのコンストラクタとプロパティを検証するテストクラス。</summary>
public class ExceptionTests
{
    /// <summary>DeviceException の各コンストラクタが正しく初期化されることを検証する。</summary>
    [Fact]
    public void DeviceExceptionConstructorsShouldWork()
    {
        var ex1 = new DeviceException();
        ex1.Message.ShouldContain("Device error");
        ex1.ErrorCode.ShouldBe(DeviceErrorCode.Failure);

        var ex2 = new DeviceException("Custom message");
        ex2.Message.ShouldBe("Custom message");
        ex2.ErrorCode.ShouldBe(DeviceErrorCode.Failure);

        var ex3 = new DeviceException("Custom message", DeviceErrorCode.Illegal);
        ex3.ErrorCode.ShouldBe(DeviceErrorCode.Illegal);

        var inner = new Exception("Inner");
        var ex4 = new DeviceException("Message", inner);
        ex4.InnerException.ShouldBe(inner);

        var ex5 = new DeviceException("Message", DeviceErrorCode.Jammed, 1234);
        ex5.ErrorCode.ShouldBe(DeviceErrorCode.Jammed);
        ex5.ErrorCodeExtended.ShouldBe(1234);
    }

    /// <summary>InsufficientCashException の各コンストラクタが正しく初期化されることを検証する。</summary>
    [Fact]
    public void InsufficientCashExceptionConstructorsShouldWork()
    {
        var ex1 = new InsufficientCashException();
        ex1.Message.ShouldContain("Insufficient cash");

        var ex2 = new InsufficientCashException("Custom message");
        ex2.Message.ShouldBe("Custom message");

        var inner = new Exception("Inner");
        var ex3 = new InsufficientCashException("Message", inner);
        ex3.InnerException.ShouldBe(inner);
    }
}
