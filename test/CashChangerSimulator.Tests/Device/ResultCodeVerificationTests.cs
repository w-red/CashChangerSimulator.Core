using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>SimulatorCashChanger の各メソッド実行後に ResultCode が正しく更新されるかを検証するテスト。</summary>
public class ResultCodeVerificationTests
{
    private readonly Inventory _inventory;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly ConfigurationProvider _configProvider;

    public ResultCodeVerificationTests()
    {
        _inventory = new Inventory();
        _hardwareStatusManager = new HardwareStatusManager();
        _configProvider = new ConfigurationProvider();
    }

    private InternalSimulatorCashChanger CreateChanger()
    {
        var deps = new SimulatorDependencies(
            _configProvider,
            _inventory,
            null,
            null,
            new DepositController(_inventory, _hardwareStatusManager),
            null,
            null,
            _hardwareStatusManager);
        var changer = new InternalSimulatorCashChanger(deps);
        changer.SkipStateVerification = true;
        return changer;
    }

    [Fact]
    public void OpenShouldSetResultCodeToSuccess()
    {
        // Arrange
        var changer = CreateChanger();
        changer.ResultCode = -1; // Reset to non-zero

        // Act
        changer.Open();

        // Assert
        changer.ResultCode.ShouldBe((int)ErrorCode.Success);
    }

    [Fact]
    public void ClaimShouldSetResultCodeToSuccess()
    {
        // Arrange
        var changer = CreateChanger();
        changer.Open();
        changer.ResultCode = -1;

        // Act
        changer.Claim(0);

        // Assert
        changer.ResultCode.ShouldBe((int)ErrorCode.Success);
    }

    [Fact]
    public void BeginDepositShouldSetResultCodeToSuccess()
    {
        // Arrange
        var changer = CreateChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;
        changer.ResultCode = -1;

        // Act
        changer.BeginDeposit();

        // Assert
        changer.ResultCode.ShouldBe((int)ErrorCode.Success);
    }

    [Fact]
    public void EndDepositShouldSetResultCodeToSuccess()
    {
        // Arrange
        var changer = CreateChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;
        changer.BeginDeposit();
        changer.FixDeposit();
        changer.ResultCode = -1;

        // Act
        changer.EndDeposit(CashDepositAction.NoChange);

        // Assert
        changer.ResultCode.ShouldBe((int)ErrorCode.Success);
    }

    [Fact]
    public void AdjustCashCountsShouldSetResultCodeToSuccess()
    {
        // Arrange
        var changer = CreateChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;
        changer.ResultCode = -1;

        // Act
        changer.AdjustCashCounts([]);

        // Assert
        changer.ResultCode.ShouldBe((int)ErrorCode.Success);
    }

    [Fact]
    public void DirectIOShouldSetResultCodeToSuccess()
    {
        // Arrange
        var changer = CreateChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;
        changer.ResultCode = -1;

        // Act
        changer.DirectIO(0, 0, new object());

        // Assert
        changer.ResultCode.ShouldBe((int)ErrorCode.Success);
    }
}
