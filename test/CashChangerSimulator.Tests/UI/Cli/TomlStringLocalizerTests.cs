using System.Globalization;
using CashChangerSimulator.UI.Cli.Localization;
using Shouldly;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class TomlStringLocalizerTests : IDisposable
{
    private readonly string _testI18nDir;

    public TomlStringLocalizerTests()
    {
        _testI18nDir = Path.Combine(Path.GetTempPath(), "CashChangerI18nTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testI18nDir);

        File.WriteAllText(Path.Combine(_testI18nDir, "cli.ja.toml"), "[messages]\ndevice_opened = \"デバイスが正常にオープンされました。\"\nfailed_to_open = \"失敗: {0}\"");
        File.WriteAllText(Path.Combine(_testI18nDir, "cli.en.toml"), "[messages]\ndevice_opened = \"Device opened successfully.\"\nfailed_to_open = \"Failed: {0}\"");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testI18nDir))
        {
            Directory.Delete(_testI18nDir, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ShouldReturnJapaneseStringWhenCultureIsJa()
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");
        var localizer = new TomlStringLocalizer(_testI18nDir);

        // Act
        var result = localizer["messages.device_opened"].Value;

        // Assert
        result.ShouldBe("デバイスが正常にオープンされました。");
    }

    [Fact]
    public void ShouldReturnEnglishStringWhenCultureIsEn()
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var localizer = new TomlStringLocalizer(_testI18nDir);

        // Act
        var result = localizer["messages.device_opened"].Value;

        // Assert
        result.ShouldBe("Device opened successfully.");
    }

    [Fact]
    public void ShouldSupportPlaceholders()
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");
        var localizer = new TomlStringLocalizer(_testI18nDir);

        // Act
        var result = localizer["messages.failed_to_open", "ErrorCode123"].Value;

        // Assert
        result.ShouldBe("失敗: ErrorCode123");
    }

    [Fact]
    public void ShouldReturnKeyNameIfNotFound()
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");
        var localizer = new TomlStringLocalizer(_testI18nDir);

        // Act
        var result = localizer["NonExistentKey"];

        // Assert
        result.Value.ShouldBe("NonExistentKey");
        result.ResourceNotFound.ShouldBeTrue();
    }
}
