using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Core.Models;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Ui.Cli;

/// <summary>CliCashService の現金操作（入出金）機能を検証するテストクラス。</summary>
public class CliCashServiceTests : CliTestBase
{
    /// <summary>入金モードでない状態で EndDeposit を呼び出した際、エラーメッセージが表示されることを検証する。</summary>
    [Fact]
    public void EndDepositShouldUpdateResultCodeAndPrintErrorMessageWhenNotInDepositMode()
    {
        var realChanger = new InternalSimulatorCashChanger(new SimulatorDependencies())
        {
        };
        realChanger.SkipStateVerification = true;
        realChanger.Open();
        realChanger.Claim(0);
        realChanger.DeviceEnabled = true;
        // 注意: BeginDeposit() を呼んでいないため、EndDeposit() は Invalid State (Illegal) となるはず。

        var cashService = new CliCashService(realChanger, _mockInventory.Object, _mockMetadata.Object, _options, _console, _localizer);

        // Act
        // CliCashService 内の EndDeposit() は、Changer の EndDeposit を呼び、例外をキャッチして UI フォーマットする。
        cashService.EndDeposit();

        // Assert
        // CLI UI サービスが PosControlException を補足し、エラー番号付きで画面出力していること
        _console.Output.ShouldContain($"[Error: {(int)ErrorCode.Illegal}");
    }

    /// <summary>在庫不足の状態で出金を試みた際、例外がキャッチされ拡張エラーコード（ErrorCode.Extended）が表示されることを検証する。</summary>
    [Fact]
    public void DispenseShouldHandlePosControlExceptionAndCheckExtendedErrorCodeWhenInventoryIsInsufficient()
    {
        // Arrange
        var realChanger = new InternalSimulatorCashChanger(new SimulatorDependencies())
        {
        };
        realChanger.SkipStateVerification = true;
        realChanger.Open();
        realChanger.Claim(0);
        realChanger.DeviceEnabled = true;

        var cashService = new CliCashService(realChanger, _mockInventory.Object, _mockMetadata.Object, _options, _console, _localizer);

        // Act
        // 1000万など、初期在庫（通常は数万円程度）を明らかに超える金額を出金しようと試みる
        cashService.Dispense(10000000);

        // Assert
        // UI に正しくエラーコードが反映されているか
        _console.Output.ShouldContain($"[Error: {(int)ErrorCode.Extended}");
    }

    [Fact]
    public void EndDepositShouldPrintSuccessWhenInvokedCorrectly()
    {
        var cashService = new CliCashService(_mockChanger.Object, _mockInventory.Object, _mockMetadata.Object, _options, _console, _localizer);

        // Act
        cashService.EndDeposit();

        // Assert
        _mockChanger.Verify(x => x.EndDeposit(CashDepositAction.Change), Times.Once);
        _console.Output.ShouldContain("completed", Case.Insensitive);
    }

    [Fact]
    public void DispenseShouldPrintSuccessWhenInvokedCorrectly()
    {
        var cashService = new CliCashService(_mockChanger.Object, _mockInventory.Object, _mockMetadata.Object, _options, _console, _localizer);

        // Act
        cashService.Dispense(500);

        // Assert
        _mockChanger.Verify(x => x.DispenseChange(500), Times.Once);
        _console.Output.ShouldContain("500");
    }

    [Fact]
    public void AdjustCashCountsShouldSucceedWithValidInput()
    {
        // Arrange
        var cashService = new CliCashService(_mockChanger.Object, _mockInventory.Object, _mockMetadata.Object, _options, _console, _localizer);
        _mockMetadata.Setup(x => x.SupportedDenominations).Returns(
        [
            new DenominationKey(1000, CurrencyCashType.Bill, "JPY")
        ]);
        
        // Act
        cashService.AdjustCashCounts("1000:5"); // "1000:5" format

        // Assert
        _mockChanger.Verify(x => x.AdjustCashCounts(It.IsAny<IEnumerable<CashCount>>()), Times.Once);
        _console.Output.ShouldContain("success", Case.Insensitive);
    }

    [Fact]
    public void AdjustCashCountsShouldPrintInvalidFormatWithInvalidInput()
    {
        var cashService = new CliCashService(_mockChanger.Object, _mockInventory.Object, _mockMetadata.Object, _options, _console, _localizer);

        // Act
        cashService.AdjustCashCounts("InvalidFormat");

        // Assert
        _console.Output.ShouldContain("Invalid", Case.Insensitive);
    }
}
