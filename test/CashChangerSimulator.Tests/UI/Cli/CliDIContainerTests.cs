using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Cli;
using Shouldly;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliDIContainerTests
{
    [Fact]
    public void InitializeShouldOverrideCurrencyCode()
    {
        // Arrange
        string[] args = ["--currency", "USD"];

        // Act
        CliDIContainer.Initialize(args);
        var serviceProvider = CliDIContainer.Resolve<IServiceProvider>();
        CliDIContainer.PostInitialize(serviceProvider, args);

        var configProvider = CliDIContainer.Resolve<ConfigurationProvider>();

        // Assert
        configProvider.Config.System.CurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void InitializeShouldDefaultToJpyWhenNoArgs()
    {
        // Arrange
        string[] args = [];

        // Act
        CliDIContainer.Initialize(args);
        var configProvider = CliDIContainer.Resolve<ConfigurationProvider>();

        // Assert
        // Assuming default in config.toml or default state is JPY
        configProvider.Config.System.CurrencyCode.ShouldBe("JPY");
    }
}
