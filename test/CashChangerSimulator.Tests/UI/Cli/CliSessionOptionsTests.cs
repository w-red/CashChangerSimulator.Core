using Shouldly;
using CashChangerSimulator.UI.Cli;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliSessionOptionsTests
{
    [Fact]
    public void PropertiesShouldSetAndGetCorrectly()
    {
        var options = new CliSessionOptions();

        options.IsAsync = true;
        options.Language = "ja";
        options.CurrencyCode = "USD";

        options.IsAsync.ShouldBeTrue();
        options.Language.ShouldBe("ja");
        options.CurrencyCode.ShouldBe("USD");
    }
}
