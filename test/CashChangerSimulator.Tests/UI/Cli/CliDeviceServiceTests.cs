using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Cli.Services;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Ui.Cli;

/// <summary>CliDeviceService の機能を検証するテストクラス。</summary>
public class CliDeviceServiceTests : CliTestBase
{
    /// <summary>デバイスがオープンされていない状態で Claim を呼び出した際、例外が適切に処理されることを検証する。</summary>
    [Fact]
    public void ClaimShouldHandlePosControlExceptionWhenDeviceIsNotOpened()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Simulation.HotStart = false;
        var realChanger = new SimulatorCashChanger(configProvider, null, null, null, null, null, null, null)
        {
            // SkipStateVerification = true // Claim 内部で _isOpen のチェックが行われ、例外になる
        };
        // Open していない状態

        var deviceService = new CliDeviceService(realChanger, _console, _localizer);

        // Act
        deviceService.Claim(1000);

        // Assert
        // UI サービスが例外をキャッチしてメッセージを出力していること
        _console.Output.ShouldContain($"[Error: {(int)ErrorCode.Closed}");
    }

    /// <summary>デバイスが占有されていない状態で有効化しようとした際、例外が適切に処理されることを検証する。</summary>
    [Fact]
    public void EnableShouldHandlePosControlExceptionWhenDeviceIsNotClaimed()
    {
        // Arrange
        var realChanger = new SimulatorCashChanger(null, null, null, null, null, null, null, null)
        {
            // SkipStateVerification = true
        };
        realChanger.Open();
        // Claim していない状態で Enable しようとする

        var deviceService = new CliDeviceService(realChanger, _console, _localizer);

        // Act
        deviceService.Enable();

        // Assert
        // 少なくともUI側でエラーのフォーマットが出力される
        _console.Output.ShouldContain("[Error: ");
    }
}
