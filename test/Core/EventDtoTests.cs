using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Core;

/// <summary>各種デバイスイベント引数（EventArgs）のプロパティアクセスとコンストラクタを検証するテストクラス。</summary>
public class EventDtoTests
{
    /// <summary>DeviceDirectIOEventArgs のプロパティが正しく初期化および取得できることを検証します。</summary>
    [Fact]
    public void DeviceDirectIOEventArgsShouldHoldValues()
    {
        // Arrange
        var obj = new object();
        var sut = new DeviceDirectIOEventArgs(123, 456, obj);

        // Assert
        sut.EventNumber.ShouldBe(123);
        sut.Data.ShouldBe(456);
        sut.EventObject.ShouldBe(obj);
    }

    /// <summary>DeviceDataEventArgs のプロパティが正しく初期化および取得できることを検証します。</summary>
    [Fact]
    public void DeviceDataEventArgsShouldHoldValues()
    {
        // Arrange
        var sut = new DeviceDataEventArgs(789);

        // Assert
        sut.Status.ShouldBe(789);
    }

    /// <summary>DeviceErrorEventArgs のプロパティが正しく初期化および取得できることを検証します。</summary>
    [Fact]
    public void DeviceErrorEventArgsShouldHoldValues()
    {
        // Arrange
        var sut = new DeviceErrorEventArgs(
            (DeviceErrorCode)1, 
            2, 
            (DeviceErrorLocus)3, 
            (DeviceErrorResponse)4);

        // Assert
        sut.ErrorCode.ShouldBe((DeviceErrorCode)1);
        sut.ErrorCodeExtended.ShouldBe(2);
        sut.ErrorLocus.ShouldBe((DeviceErrorLocus)3);
        sut.ErrorResponse.ShouldBe((DeviceErrorResponse)4);

        // Verify setter
        sut.ErrorResponse = (DeviceErrorResponse)100;
        sut.ErrorResponse.ShouldBe((DeviceErrorResponse)100);
    }

    /// <summary>DeviceOutputCompleteEventArgs のプロパティが正しく初期化および取得できることを検証します。</summary>
    [Fact]
    public void DeviceOutputCompleteEventArgsShouldHoldValues()
    {
        // Arrange
        var sut = new DeviceOutputCompleteEventArgs(55);

        // Assert
        sut.OutputId.ShouldBe(55);
    }

    /// <summary>DeviceStatusUpdateEventArgs のプロパティが正しく初期化および取得できることを検証します。</summary>
    [Fact]
    public void DeviceStatusUpdateEventArgsShouldHoldValues()
    {
        // Arrange
        var sut = new DeviceStatusUpdateEventArgs(200);

        // Assert
        sut.Status.ShouldBe(200);
    }
}
