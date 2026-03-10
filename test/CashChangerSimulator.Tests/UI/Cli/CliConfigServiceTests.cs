using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Cli.Services;
using Shouldly;

namespace CashChangerSimulator.Tests.Ui.Cli;

/// <summary>CliConfigService の設定操作機能を検証するテストクラス。</summary>
public class CliConfigServiceTests : CliTestBase
{
    private readonly CliConfigService _configService;
    private readonly ConfigurationProvider _configProvider;

    public CliConfigServiceTests() : base()
    {
        _configProvider = new ConfigurationProvider();
        _configService = new CliConfigService(_configProvider, _console, _localizer);
    }

    [Fact]
    public void ListShouldPrintConfigurationProperties()
    {
        // Act
        _configService.List();

        // Assert
        var output = _console.Output;
        output.ShouldContain("Simulation.HotStart");
        output.ShouldContain("System.CurrencyCode");
    }

    [Fact]
    public void GetShouldPrintSpecifiedProperty()
    {
        // Act
        _configService.Get("System.CurrencyCode");

        // Assert
        _console.Output.ShouldContain("System.CurrencyCode =");
    }

    [Fact]
    public void SetShouldUpdatePropertyAndNotifyUser()
    {
        // Act
        _configService.Set("System.CurrencyCode", "USD");

        // Assert
        _configProvider.Config.System.CurrencyCode.ShouldBe("USD");
        _console.Output.ShouldContain("updated");
    }

    [Fact]
    public void SetInvalidKeyShouldPrintErrorMessage()
    {
        // Act
        _configService.Set("Invalid.Key", "value");

        // Assert
        _console.Output.ShouldContain("Invalid config key");
    }
}
