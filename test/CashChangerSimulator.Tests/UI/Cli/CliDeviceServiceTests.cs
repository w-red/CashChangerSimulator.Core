using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Cli.Services;
using Microsoft.PointOfService;
using Shouldly;
using System;
using Xunit;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliDeviceServiceTests : CliTestBase
{
    [Fact]
    public void ClaimShouldHandlePosControlExceptionWhenDeviceIsNotOpened()
    {
        // Arrange
        var realChanger = new SimulatorCashChanger(null, null, null, null, null, null, null, null)
        {
            SkipStateVerification = true // Claim 内部で _isOpen のチェックが行われ、例外になる
        };
        // Open していない状態

        var deviceService = new CliDeviceService(realChanger, _console, _localizer);

        // Act
        deviceService.Claim(1000);

        // Assert
        // Claim メソッド内で _isOpen=false を検知して Closed エラーがスローされ、
        // SimulatorCashChanger の ResultCode が更新されるはず
        realChanger.ResultCode.ShouldBe((int)ErrorCode.Closed);

        // UI サービスが例外をキャッチしてメッセージを出力していること
        _console.Output.ShouldContain($"[Error: {(int)ErrorCode.Closed}");
    }

    [Fact]
    public void EnableShouldHandlePosControlExceptionWhenDeviceIsNotClaimed()
    {
        // Arrange
        var realChanger = new SimulatorCashChanger(null, null, null, null, null, null, null, null)
        {
            SkipStateVerification = true,
            StrictDeviceEnabledCheck = true
        };
        realChanger.Open();
        // Claim していない状態で Enable しようとする

        var deviceService = new CliDeviceService(realChanger, _console, _localizer);

        // Act
        deviceService.Enable();

        // Assert
        // DeviceEnabled=true にしようとすると、CashChangerBasic/SimulatorCashChanger 側で
        // IsClaimed チェックに引っかかり、NoHardware や Illegal などのエラーになり ResultCode が更新される
        realChanger.ResultCode.ShouldNotBe((int)ErrorCode.Success);
        
        // 少なくともUI側でエラーのフォーマットが出力される
        _console.Output.ShouldContain("[Error: ");
    }
}
