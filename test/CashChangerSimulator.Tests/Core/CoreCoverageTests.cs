using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

public class CoreCoverageTests
{
    [Fact]
    public void SimulatorServices_Resolve_ShouldThrowWhenNotFound()
    {
        // Arrange
        SimulatorServices.Provider = null;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => SimulatorServices.Resolve<IAsyncResult>());
    }

    [Fact]
    public void SimulatorServices_TryResolve_ShouldReturnNullWhenProviderMissing()
    {
        // Arrange
        SimulatorServices.Provider = null;

        // Act
        var result = SimulatorServices.TryResolve<IAsyncResult>();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TransactionHistory_Dispose_ShouldWork()
    {
        // Arrange
        var history = new TransactionHistory();

        // Act
        history.Dispose();
        
        // Assert - just ensuring coverage of Dispose
        // In a real scenario, we might check if event subscriptions are cleared if possible.
    }

    [Fact]
    public void CurrencyMetadataProvider_GetSymbol_ShouldWork()
    {
        // Arrange
        var config = new CashChangerSimulator.Core.Configuration.ConfigurationProvider();
        var provider = new CashChangerSimulator.Core.Services.CurrencyMetadataProvider(config);

        // Act
        var symbol = provider.Symbol;

        // Assert
        symbol.ShouldNotBeNull();
    }

    [Fact]
    public void CurrencyMetadataProvider_GetDenominationName_NonJpy_ShouldWork()
    {
        // Arrange
        var config = new CashChangerSimulator.Core.Configuration.ConfigurationProvider();
        // 設定を直接書き換える（本来はファイルから読み込むが、テスト用にメンバにアクセスできるか確認）
        config.Config.System.CurrencyCode = "USD";
        
        var provider = new CashChangerSimulator.Core.Services.CurrencyMetadataProvider(config);

        // Act
        var usdKey = new DenominationKey(1.00m, CurrencyCashType.Coin, "USD");
        var name = provider.GetDenominationName(usdKey);

        // Assert
        name.ShouldBe("$1.00 Coin");
    }
}
