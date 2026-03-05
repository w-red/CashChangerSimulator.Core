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

        File.WriteAllText(Path.Combine(_testI18nDir, "ja.toml"), "DeviceOpened = \"デバイスが正常にオープンされました。\"\nFailedToOpen = \"失敗: {0}\"");
        File.WriteAllText(Path.Combine(_testI18nDir, "en.toml"), "DeviceOpened = \"Device opened successfully.\"\nFailedToOpen = \"Failed: {0}\"");
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
        var result = localizer["DeviceOpened"].Value;

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
        var result = localizer["DeviceOpened"].Value;

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
        var result = localizer["FailedToOpen", "ErrorCode123"].Value;

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
