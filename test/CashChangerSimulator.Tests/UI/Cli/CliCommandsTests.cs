using Cocona;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Cli;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Configuration;
using Moq;
using Xunit;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using Shouldly;
using System.IO;
using System.Globalization;
using CashChangerSimulator.Device.Services;
using Spectre.Console;
using Spectre.Console.Testing;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Localization;
using CashChangerSimulator.UI.Cli.Services;
using System.Linq;
using System;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliCommandsTests : CliTestBase
{
    private readonly CliCommands _commands;

    public CliCommandsTests() : base()
    {
        var mockConfigProvider = new Mock<ConfigurationProvider>(new object?[] { null });
        var deviceService = new CliDeviceService(_mockChanger.Object, _console, _localizer);
        var cashService = new CliCashService(_mockChanger.Object, _mockInventory.Object, _mockMetadata.Object, _options, _console, _localizer);
        var configService = new CliConfigService(mockConfigProvider.Object, _console, _localizer);
        var viewService = new CliViewService(_mockChanger.Object, _mockInventory.Object, _mockMetadata.Object, _mockHistory.Object, _console, _localizer);
        var scriptService = new CliScriptService(_mockScriptService.Object, _console, _localizer);

        _commands = new CliCommands(
            _mockChanger.Object,
            deviceService,
            cashService,
            configService,
            viewService,
            scriptService,
            _console,
            _localizer);
    }

    [Fact]
    public void HandleExceptionShouldPrintDetailedErrorCodeAndHint()
    {
        // Arrange
        _mockChanger.Setup(x => x.Open()).Throws(new PosControlException("Test message", ErrorCode.Closed, 1));

        // Act
        _commands.Open();

        // Assert
        _console.Output.ShouldContain("[Error: 101 (1)] Test message");
        _console.Output.ShouldContain("Hint: Please open the device first.");
    }

    [Fact]
    public void StatusShouldReflectLanguageSetting()
    {
        // Arrange
        _options.Language = "en";

        // Act
        _commands.Status();

        // Assert
        _console.Output.ShouldContain("Device Status");
        _console.Output.ShouldContain("State");
        _console.Output.ShouldContain("Enabled");
    }

    [Fact]
    public void OpenShouldCallChangerOpenAndPrintLocalizedMessage()
    {
        // Act
        _commands.Open();

        // Assert
        _mockChanger.Verify(x => x.Open(), Times.Once);
        _console.Output.ShouldContain("Device opened successfully.");
    }

    [Fact]
    public void ReadCashCountsShouldPrintLocalizedTable()
    {
        // Arrange
        var mockCashCounts = new CashCounts(
        [
            new CashCount(CashCountType.Bill, 1000, 10),
        ], false);
        _mockChanger.Setup(x => x.ReadCashCounts()).Returns(mockCashCounts);
        _mockInventory.Setup(x => x.CalculateTotal("JPY")).Returns(10000m);

        // Act
        _commands.ReadCashCounts();

        // Assert
        _console.Output.ShouldContain("Cash counts updated");
        _console.Output.ShouldContain("Denomination");
        _console.Output.ShouldContain("Count");
    }

    [Fact]
    public void DepositShouldRespectAsyncFlagAndPrintLocalizedMessage()
    {
        // Arrange
        _options.IsAsync = true;

        // Act
        _commands.Deposit(1000);

        // Assert
        _mockChanger.Verify(x => x.BeginDeposit(), Times.Once);
        _console.Output.ShouldContain("Depositing 1000 (Async: True)");
        _console.Output.ShouldContain("Deposit started in async mode");
    }

    [Fact]
    public void DepositShouldRespectSyncFlagAndPrintLocalizedMessage()
    {
        // Arrange
        _options.IsAsync = false;

        // Act
        _commands.Deposit(1000);

        // Assert
        _mockChanger.Verify(x => x.BeginDeposit(), Times.Once);
        _mockChanger.Verify(x => x.FixDeposit(), Times.Once);
        _console.Output.ShouldContain("Deposit completed.");
    }

    [Fact]
    public void DispenseShouldPrintLocalizedMessage()
    {
        // Act
        _commands.Dispense(500);

        // Assert
        _mockChanger.Verify(x => x.DispenseChange(500), Times.Once);
        _console.Output.ShouldContain("Dispensed 500 successfully.");
    }

    [Fact]
    public void ConfigGetShouldReturnCorrectValue()
    {
        // Arrange: テスト用の config.toml ファイルを作成し、ConfigurationProvider を構築
        var tempConfigPath = Path.Combine(_testI18nDir, "test_config.toml");
        File.WriteAllText(tempConfigPath, @"
[System]
CurrencyCode = 'EUR'
");
        var configProvider = new ConfigurationProvider(tempConfigPath);
        var configService = new CliConfigService(configProvider, _console, _localizer);

        // Act
        configService.Get("System.CurrencyCode");

        // Assert
        _console.Output.ShouldContain("System.CurrencyCode");
        _console.Output.ShouldContain("EUR");
    }

    [Fact]
    public void ConfigSetShouldUpdateValue()
    {
        // Arrange: テスト用の config.toml ファイルを作成し、ConfigurationProvider を構築
        var tempConfigPath = Path.Combine(_testI18nDir, "test_config_set.toml");
        File.WriteAllText(tempConfigPath, @"
[System]
CurrencyCode = 'JPY'
");
        var configProvider = new ConfigurationProvider(tempConfigPath);
        var configService = new CliConfigService(configProvider, _console, _localizer);

        // Act
        configService.Set("System.CurrencyCode", "USD");

        // Assert
        configProvider.Config.System.CurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void HelpShouldPrintLocalizedHeaders()
    {
        // Arrange
        _options.Language = "en";

        // Act
        _commands.Help();

        // Assert
        _console.Output.ShouldContain("Command", Case.Insensitive);
        _console.Output.ShouldContain("open", Case.Insensitive);
        _console.Output.ShouldContain("claim", Case.Insensitive);
        _console.Output.ShouldContain("status", Case.Insensitive);
    }

    [Fact]
    public void AsyncErrorEventShouldPrintErrorMessage()
    {
        // Arrange
        var mockArgs = new DeviceErrorEventArgs(ErrorCode.Closed, 0, ErrorLocus.Output, ErrorResponse.Clear);

        // Act
        // CliCommands のメソッド HandleAsyncError を直接呼び出し、非同期エラーイベント受信時の動作を検証する。
        _commands.HandleAsyncError(_mockChanger.Object, mockArgs);

        // Assert
        _console.Output.ShouldContain("[Async Error: 101 (0)] Async operation failed", Case.Insensitive);
        _console.Output.ShouldContain("Hint: Please open the device first.");
    }
}
