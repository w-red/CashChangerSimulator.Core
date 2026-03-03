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
using Spectre.Console;
using Spectre.Console.Testing;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Localization;
using CashChangerSimulator.UI.Cli.Services;
using System.Linq;
using System;
using CashChangerSimulator.Device.Services;

namespace CashChangerSimulator.Tests.Ui.Cli;

/// <summary>CliCommands の各種コマンド機能を検証するテストクラス。</summary>
public class CliCommandsTests : CliTestBase
{
    private readonly CliCommands _commands;

    public CliCommandsTests() : base()
    {
        var mockConfigProvider = new Mock<ConfigurationProvider>();
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
        _mockChanger.Setup(x => x.Open()).Throws(new PosControlException("Test message", ErrorCode.Closed, 1));
        _commands.Open();
        _console.Output.ShouldContain("[Error: 101 (1)] Test message");
    }

    [Fact]
    public void StatusShouldReflectLanguageSetting()
    {
        _options.Language = "en";
        _commands.Status();
        _console.Output.ShouldContain("Device Status");
    }

    [Fact]
    public void OpenShouldCallChangerOpen()
    {
        _commands.Open();
        _mockChanger.Verify(x => x.Open(), Times.Once);
    }

    [Fact]
    public void ReadCashCountsShouldPrintTable()
    {
        var mockCashCounts = new CashCounts([new CashCount(CashCountType.Bill, 1000, 10)], false);
        _mockChanger.Setup(x => x.ReadCashCounts()).Returns(mockCashCounts);
        _commands.ReadCashCounts();
        _console.Output.ShouldContain("Cash counts updated");
    }

    [Fact]
    public void HelpShouldPrintHeaders()
    {
        _options.Language = "en";
        _commands.Help();
        _console.Output.ShouldContain("Command", Case.Insensitive);
    }
}
