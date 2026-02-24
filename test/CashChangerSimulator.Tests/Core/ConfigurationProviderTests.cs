namespace CashChangerSimulator.Tests.Core;

using CashChangerSimulator.Core.Configuration;
using R3;
using Shouldly;
using Xunit;

/// <summary>ConfigurationProvider の動作を検証するテスト。</summary>
public class ConfigurationProviderTests
{
    /// <summary>設定の初期読み込みと再読み込みイベントの発火を検証する。</summary>
    [Fact]
    public void ReloadShouldUpdateConfigAndFireEvent()
    {
        // Arrange
        var provider = new ConfigurationProvider();
        provider.Config.ShouldNotBeNull();

        bool reloadedFired = false;
        using var _ = provider.Reloaded.Subscribe(_ => reloadedFired = true);

        // Act
        provider.Reload();

        // Assert
        reloadedFired.ShouldBeTrue();
        provider.Config.ShouldNotBeNull();
    }
}
