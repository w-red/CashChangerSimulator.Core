using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.PosForDotNet.Models;
using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>SimulatorCashChanger の各メソッド実行後に ResultCode が正しく更新されるかを検証するテスト。</summary>
public class ResultCodeVerificationTests
{
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly ConfigurationProvider configProvider;

    public ResultCodeVerificationTests()
    {
        inventory = Inventory.Create();
        hardwareStatusManager = HardwareStatusManager.Create();
        configProvider = new ConfigurationProvider();
    }

    private InternalSimulatorCashChanger CreateChanger()
    {
        var deps = new SimulatorDependencies(
            configProvider,
            inventory,
            null,
            null,
            new DepositController(new Mock<CashChangerManager>(inventory, new TransactionHistory(), configProvider).Object, inventory, hardwareStatusManager, configProvider, new LoggerFactory()),
            null,
            null,
            hardwareStatusManager);
        var changer = new InternalSimulatorCashChanger(deps)
        {
            SkipStateVerification = true
        };
        return changer;
    }

    /// <summary>Open 実行後に ResultCode が Success にセットされることを検証します。</summary>
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

    /// <summary>Claim 実行後に ResultCode が Success にセットされることを検証します。</summary>
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

    /// <summary>BeginDeposit 実行後に ResultCode が Success にセットされることを検証します。</summary>
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

    /// <summary>EndDeposit 実行後に ResultCode が Success にセットされることを検証します。</summary>
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

    /// <summary>AdjustCashCounts 実行後に ResultCode が Success にセットされることを検証します。</summary>
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
        changer.AdjustCashCounts(string.Empty);

        // Assert
        changer.ResultCode.ShouldBe((int)ErrorCode.Success);
    }

    /// <summary>DirectIO 実行後に ResultCode が Success にセットされることを検証します。</summary>
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
