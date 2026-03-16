using System.Globalization;
using CashChangerSimulator.UI.Cli.Localization;
using Shouldly;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliLocalizationTests
{
    private readonly string _i18nPath;

    public CliLocalizationTests()
    {
        // Path to the i18n directory in the source project
        _i18nPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "i18n");
        
        // Ensure the directory exists (it should be copied by build or we might need to point to src)
        if (!Directory.Exists(_i18nPath))
        {
             // Fallback for local execution if not copied to output
             var projectPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "CashChangerSimulator.UI.Cli", "i18n"));
             if (Directory.Exists(projectPath)) _i18nPath = projectPath;
        }
    }

    [Fact]
    public void ShouldReturnJapaneseWelcomeMessage()
    {
        // Arrange
        var localizer = new TomlStringLocalizer(_i18nPath);
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");

            // Act
            var welcome = localizer["messages.welcome"];

            // Assert
            welcome.Value.ShouldBe("自動釣銭機シミュレータ CLI");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void ShouldReturnFallbackEnglishMessageWhenMissing()
    {
        // Arrange
        var localizer = new TomlStringLocalizer(_i18nPath);
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR"); // Non-existent

            // Act
            var welcome = localizer["messages.welcome"];

            // Assert
            welcome.Value.ShouldBe("Cash Changer Simulator CLI"); // Content of cli.en.toml
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
