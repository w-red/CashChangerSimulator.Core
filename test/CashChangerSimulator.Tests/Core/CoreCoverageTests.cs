using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>カバレッジ向上のため、サービス解決や履歴破棄、メタデータ取得等の個別機能を検証するテストクラス。</summary>
public class CoreCoverageTests
{
    /// <summary>SimulatorServices において、未登録のサービスを解決しようとした際に例外が発生することを検証します。</summary>
    [Fact]
    public void SimulatorServices_Resolve_ShouldThrowWhenNotFound()
    {
        // Arrange
        SimulatorServices.Provider = null;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => SimulatorServices.Resolve<IAsyncResult>());
    }

    /// <summary>SimulatorServices において、Provider が未設定の場合に TryResolve が null を返すことを検証します。</summary>
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

    /// <summary>TransactionHistory の破棄（Dispose）がエラーなく実行できることを検証します。</summary>
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

    /// <summary>CurrencyMetadataProvider がデフォルト設定から通貨記号を正しく取得できることを検証します。</summary>
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

    /// <summary>CurrencyMetadataProvider が JPY 以外の通貨（USD等）に対して正しい金種名称を生成することを検証します。</summary>
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
