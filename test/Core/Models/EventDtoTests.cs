using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using Shouldly;
using PosSharp.Abstractions;

namespace CashChangerSimulator.Tests.Core.Models;

/// <summary>各種デバイスイベント引数(EventArgs)のプロパティアクセスとコンストラクタを検証するテストクラス。</summary>
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
        sut.ObjectData.ShouldBe(obj);
    }

    /// <summary>UposDataEventArgs のプロパティが正しく初期化および取得できることを検証します。</summary>
    [Fact]
    public void UposDataEventArgsShouldHoldValues()
    {
        // Arrange
        var sut = new UposDataEventArgs(789);

        // Assert
        sut.Status.ShouldBe(789);
    }

    /// <summary>UposErrorEventArgs のプロパティが正しく初期化および取得できることを検証します。</summary>
    [Fact]
    public void UposErrorEventArgsShouldHoldValues()
    {
        // Arrange
        var sut = new UposErrorEventArgs(
            (UposErrorCode)1,
            2,
            (UposErrorLocus)3,
            (UposErrorResponse)4);

        // Assert
        sut.ErrorCode.ShouldBe((UposErrorCode)1);
        sut.ExtendedErrorCode.ShouldBe(2);
        sut.ErrorLocus.ShouldBe((UposErrorLocus)3);
        sut.ErrorResponse.ShouldBe((UposErrorResponse)4);
    }

    /// <summary>UposOutputCompleteEventArgs のプロパティが正しく初期化および取得できることを検証します。</summary>
    [Fact]
    public void UposOutputCompleteEventArgsShouldHoldValues()
    {
        // Arrange
        var sut = new UposOutputCompleteEventArgs(55);

        // Assert
        sut.OutputId.ShouldBe(55);
    }

    /// <summary>UposStatusUpdateEventArgs のプロパティが正しく初期化および取得できることを検証します。</summary>
    [Fact]
    public void UposStatusUpdateEventArgsShouldHoldValues()
    {
        // Arrange
        var sut = new UposStatusUpdateEventArgs(200);

        // Assert
        sut.Status.ShouldBe(200);
    }
}