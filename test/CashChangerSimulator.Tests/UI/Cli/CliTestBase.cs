using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Cli;
using CashChangerSimulator.UI.Cli.Localization;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Core.Services;
using Moq;
using R3;
using Spectre.Console.Testing;
using System;
using System.Globalization;
using System.IO;

namespace CashChangerSimulator.Tests.Ui.Cli;

/// <summary>CLI 関連のテストで共通して使用する基底クラス。</summary>
public abstract class CliTestBase : IDisposable
{
    protected readonly string _testI18nDir;
    protected readonly Mock<SimulatorCashChanger> _mockChanger;
    protected readonly Mock<Inventory> _mockInventory;
    protected readonly Mock<ICurrencyMetadataProvider> _mockMetadata;
    protected readonly Mock<TransactionHistory> _mockHistory;
    protected readonly Mock<IScriptExecutionService> _mockScriptService;
    protected readonly TestConsole _console;
    protected readonly IStringLocalizer _localizer;
    protected readonly CliSessionOptions _options;

    public CliTestBase()
    {
        _testI18nDir = Path.Combine(Path.GetTempPath(), "CliCommandsI18nTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testI18nDir);
        File.WriteAllText(Path.Combine(_testI18nDir, "en.toml"), @"
DeviceOpened = 'Device opened successfully.'
DeviceClosed = 'Device closed successfully.'
DeviceClaimed = 'Device claimed successfully.'
DeviceReleased = 'Device released successfully.'
DeviceEnabled = 'Device enabled successfully.'
DeviceDisabled = 'Device disabled successfully.'
DeviceNotClaimed = 'Device must be claimed before enabling.'
StatusHeader = 'Device Status'
StateLabel = 'State'
EnabledLabel = 'Enabled'
InventoryHeader = 'Inventory'
DenominationLabel = 'Denomination'
CountLabel = 'Count'
AmountLabel = 'Amount'
TotalCaption = 'Total'
AvailableCommands = 'Available commands'
CommandLabel = 'Command'
DescriptionLabel = 'Description'
TransactionHistoryHeader = 'Recent Transactions (up to {0})'
TimestampLabel = 'Timestamp'
TypeLabel = 'Type'
CurrencyLabel = 'Currency'
CashCountsUpdated = 'Cash counts updated'
DepositStarted = 'Depositing {0} (Async: {1})...'
DepositCompleted = 'Deposit completed.'
DepositAsyncWarning = 'Deposit started in async mode'
DepositFixed = 'Deposit fixed.'
EndDepositCompleted = 'EndDeposit completed.'
DispensedSuccess = 'Dispensed {0} successfully.'
HelpDescription = 'Show this help'
ExitDescription = 'Exit'
ErrorFormat = ""[red][[{0}: {1} ({2})]] {3}[/]""
HintFormat = ""[yellow]Hint: {0}[/]""
ErrorHint_Closed = ""Please open the device first.""
ConfigHeader = 'Configuration'
ConfigSaved = 'Config saved.'
ConfigUpdated = ""Updated configuration '{0}' to '{1}'""
InvalidConfigKey = ""Invalid config key '{0}'""
");
        File.WriteAllText(Path.Combine(_testI18nDir, "ja.toml"), "DeviceOpened = 'デバイスオープン'");

        _localizer = new TomlStringLocalizer(_testI18nDir);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        _mockChanger = new Mock<SimulatorCashChanger>();
        _mockInventory = new Mock<Inventory>();
        _mockMetadata = new Mock<ICurrencyMetadataProvider>();
        _mockHistory = new Mock<TransactionHistory>();
        _mockScriptService = new Mock<IScriptExecutionService>();
        _console = new TestConsole();
        _options = new CliSessionOptions();

        // Default valid setups
        _mockMetadata.Setup(x => x.SupportedDenominations).Returns([]);
        _mockMetadata.Setup(x => x.CurrencyCode).Returns("JPY");
        _mockMetadata.Setup(x => x.SymbolPrefix).Returns(new ReactiveProperty<string>("¥"));
        _mockMetadata.Setup(x => x.SymbolSuffix).Returns(new ReactiveProperty<string>(""));
        _mockChanger.Setup(x => x.State).Returns(Microsoft.PointOfService.ControlState.Idle);
        _mockChanger.Setup(x => x.DeviceEnabled).Returns(true);
    }

    public virtual void Dispose()
    {
        if (Directory.Exists(_testI18nDir))
        {
            Directory.Delete(_testI18nDir, true);
        }
    }
}
