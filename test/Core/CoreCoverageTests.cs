using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Moq;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Core;

/// <summary>カバレッジ向上のため、サービス解決や履歴破棄、メタデータ取得等の個別機能を検証するテストクラス。</summary>
public class CoreCoverageTests
{
    /// <summary>SimulatorServices において、未登録のサービスを解決しようとした際に例外が発生することを検証します。</summary>
    [Fact]
    public void SimulatorServicesResolveShouldThrowWhenNotFound()
    {
        // Arrange
        SimulatorServices.Provider = null;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => SimulatorServices.Resolve<IAsyncResult>());
    }

    /// <summary>SimulatorServices において、Provider が未設定の場合に TryResolve が null を返すことを検証します。</summary>
    [Fact]
    public void SimulatorServicesTryResolveShouldReturnNullWhenProviderMissing()
    {
        // Arrange
        SimulatorServices.Provider = null;

        // Act
        var result = SimulatorServices.TryResolve<IAsyncResult>();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>SimulatorServices において、Provider が設定されている場合にサービスを解決できることを検証します。</summary>
    [Fact]
    public void SimulatorServicesTryResolveWhenProviderSetShouldWork()
    {
        // Arrange
        var mockProvider = new Mock<ISimulatorServiceProvider>();
        var mockService = new Mock<IAsyncResult>();
        mockProvider.Setup(p => p.Resolve<IAsyncResult>()).Returns(mockService.Object);
        SimulatorServices.Provider = mockProvider.Object;

        // Act
        var result = SimulatorServices.TryResolve<IAsyncResult>();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(mockService.Object);
    }

    /// <summary>TransactionHistory の破棄（Dispose）がエラーなく実行できることを検証します。</summary>
    [Fact]
    public void TransactionHistoryDisposeShouldWork()
    {
        // Arrange
        var history = new TransactionHistory();

        // Act
        history.Dispose();

        // Assert - just ensuring coverage of Dispose
    }

    /// <summary>CurrencyMetadataProvider がデフォルト設定から通貨記号を正しく取得できることを検証します。</summary>
    [Fact]
    public void CurrencyMetadataProviderGetSymbolShouldWork()
    {
        // Arrange
        var config = new ConfigurationProvider();
        var provider = new CurrencyMetadataProvider(config);

        // Act
        var symbol = provider.Symbol;

        // Assert
        symbol.ShouldNotBeNull();
    }

    /// <summary>CurrencyMetadataProvider が JPY 以外の通貨（USD等）に対して正しい金種名称を生成することを検証します。</summary>
    [Fact]
    public void CurrencyMetadataProviderGetDenominationNameNonJpyShouldWork()
    {
        // Arrange
        var config = new ConfigurationProvider();

        // 設定を直接書き換える
        config.Config.System.CurrencyCode = "USD";

        var provider = new CurrencyMetadataProvider(config);

        // Act
        var usdKey = new DenominationKey(1.00m, CurrencyCashType.Coin, "USD");
        var name = provider.GetDenominationName(usdKey);

        // Assert
        name.ShouldBe("$1.00 Coin");
    }

    /// <summary>OverallStatusAggregatorProvider のライフサイクルとモニター変更の通知を検証します。</summary>
    [Fact]
    public void OverallStatusAggregatorProviderLifecycleShouldWork()
    {
        // Arrange
        var config = new ConfigurationProvider();
        var inventory = new Inventory();
        var metadata = new CurrencyMetadataProvider(config);
        var monitorsProvider = new MonitorsProvider(inventory, config, metadata);

        // AggregatorProvider requires a MonitorsProvider
        using var provider = new OverallStatusAggregatorProvider(monitorsProvider);

        // Act & Assert
        provider.Aggregator.ShouldNotBeNull();
    }

    /// <summary>CurrencyMetadataProvider が JPY 以外の通貨（USD等）に対して、銀貨（Coin）と紙幣（Bill）の名称を正しく使い分けることを検証します。</summary>
    [Fact]
    public void CurrencyMetadataProviderGetDenominationNameBillCoinMappingShouldWork()
    {
        // Arrange
        var config = new ConfigurationProvider();
        config.Config.System.CurrencyCode = "USD";
        var provider = new CurrencyMetadataProvider(config);

        // Act & Assert
        provider.GetDenominationName(new DenominationKey(1m, CurrencyCashType.Coin, "USD")).ShouldBe("$1 Coin");
        provider.GetDenominationName(new DenominationKey(1m, CurrencyCashType.Bill, "USD")).ShouldBe("$1 Bill");
        provider.GetDenominationName(new DenominationKey(0.25m, CurrencyCashType.Coin, "USD")).ShouldBe("25¢ Coin");
        provider.GetDenominationName(new DenominationKey(10m, CurrencyCashType.Bill, "USD")).ShouldBe("$10 Bill");
    }

    /// <summary>MonitorsProvider.UpdateThresholdsFromConfig において、現在の金種リストに存在しない設定が含まれている場合の動作を検証します。</summary>
    [Fact]
    public void MonitorsProviderUpdateThresholdsWithMissingDenominationShouldNotCrash()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        var inventory = new Inventory();
        var metadata = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(inventory, configProvider, metadata);

        // 既存のモニターの中の1つの設定を構成から削除する
        var firstMonitor = monitorsProvider.Monitors[0];
        var keyStr = firstMonitor.Key.ToDenominationString();
        var currency = configProvider.Config.System.CurrencyCode ?? "JPY";
        configProvider.Config.Inventory[currency].Denominations.Remove(keyStr);

        // Act & Assert
        // setting が見つからないため、else ブロック (GetDenominationSetting) を通るはず
        Should.NotThrow(() => monitorsProvider.UpdateThresholdsFromConfig(configProvider.Config));
    }

    /// <summary>OverallStatusAggregator が複数のモニターの状態を正しく集約し、ニアフルや満杯等のステータス遷移を正しく判定できるか検証します。</summary>
    [Fact]
    public void OverallStatusAggregatorAggregationPathsShouldWork()
    {
        // Arrange
        var inventory = new Inventory();
        var key1 = new DenominationKey(100m, CurrencyCashType.Coin, "JPY");
        var key2 = new DenominationKey(500m, CurrencyCashType.Coin, "JPY");
        inventory.SetCount(key1, 0);
        inventory.SetCount(key2, 0);

        // Monitors with thresholds: NearEmpty=5, NearFull=50, Full=100
        var m1 = new CashStatusMonitor(inventory, key1, 5, 50, 100, true);
        var m2 = new CashStatusMonitor(inventory, key2, 5, 50, 100, true);
        var monitors = new List<CashStatusMonitor> { m1, m2 };

        using var aggregator = new OverallStatusAggregator(monitors);

        // 初期：空 (Empty) - 両方の残高が0
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.Empty);
        aggregator.FullStatus.CurrentValue.ShouldBe(CashStatus.Normal);

        // 少量：正常 (Normal)
        inventory.SetCount(key1, 10);
        inventory.SetCount(key2, 10);
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.Normal);

        // 一方がニアフル：ニアフル集約 (FullStatus -> NearFull)
        inventory.SetCount(key1, 60);
        aggregator.FullStatus.CurrentValue.ShouldBe(CashStatus.NearFull);

        // 両方がニアフル
        inventory.SetCount(key2, 60);
        aggregator.FullStatus.CurrentValue.ShouldBe(CashStatus.NearFull);

        // 一方が満杯：満杯集約 (FullStatus -> Full)
        inventory.SetCount(key1, 100);
        aggregator.FullStatus.CurrentValue.ShouldBe(CashStatus.Full);
    }

    /// <summary>HistoryPersistenceService において、ファイルが存在しない場合や無効なバイナリ形式の場合に安全に空の履歴を返すことを検証します。</summary>
    [Fact]
    public void HistoryPersistenceServiceLoadEdgeCasesShouldWork()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"history_test_{Guid.NewGuid()}.bin");
        var history = new TransactionHistory();
        using var service = new HistoryPersistenceService(history, tempFile);

        try
        {
            // 1. ファイルなし
            var state1 = service.Load();
            state1.Entries.ShouldBeEmpty();

            // 2. 無効なバイナリ
            File.WriteAllText(tempFile, "invalid format data");
            var state2 = service.Load();
            state2.Entries.ShouldBeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    /// <summary>SimulatorServices において、Provider.Resolve が例外をスローした場合に TryResolve が null を返すことを検証します。</summary>
    [Fact]
    public void SimulatorServicesTryResolveWithExceptionShouldReturnNull()
    {
        // Arrange
        var mockProvider = new Mock<ISimulatorServiceProvider>();
        mockProvider.Setup(p => p.Resolve<IAsyncResult>()).Throws<InvalidOperationException>();
        SimulatorServices.Provider = mockProvider.Object;

        // Act
        var result = SimulatorServices.TryResolve<IAsyncResult>();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>OverallStatusAggregator が空状態（Empty）やニアエンプティ（NearEmpty）を正しく判定できるか検証します。</summary>
    [Fact]
    public void OverallStatusAggregatorEmptyAndLowStatusShouldWork()
    {
        // Arrange
        var inventory = new Inventory();
        var key = new DenominationKey(100m, CurrencyCashType.Coin, "JPY");
        inventory.SetCount(key, 0);

        // NearEmpty=5
        var m1 = new CashStatusMonitor(inventory, key, 5, 50, 100, true);
        var monitors = new List<CashStatusMonitor> { m1 };

        using var aggregator = new OverallStatusAggregator(monitors);

        // 1. 最初は空 (Empty)
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.Empty);

        // 2. ニアエンプティ：1個以上〜しきい値以下 (NearEmpty)
        inventory.SetCount(key, 3);
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.NearEmpty);

        // 3. 正常：しきい値超 (Normal)
        inventory.SetCount(key, 10);
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.Normal);
    }

    /// <summary>HistoryPersistenceService において、ファイルの読み取り権限がない場合に空の履歴を返すことを検証します。</summary>
    [Fact]
    public void HistoryPersistenceServiceLoadUnauthorizedAccessShouldReturnEmpty()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"history_unauth_{Guid.NewGuid()}.bin");
        File.WriteAllBytes(tempFile, [1, 2, 3]);

        // ディレクトリをファイル名として指定することで（あるいはReadOnlyでも）例外を誘発
        // ここでは、読み取り不可能な場所をシミュレート
        using var service = new HistoryPersistenceService(new TransactionHistory(), tempFile);

        try
        {
            // Windowsでファイル属性を ReadOnly に設定して擬似的に例外を狙う（一部環境では UnauthorizedAccessException になる）
            // または、読み取りをブロックする形で IO 例外を狙う
            using var fs = new FileStream(tempFile, FileMode.Open, FileAccess.Write, FileShare.None);
            
            // Act
            var state = service.Load();
            
            // Assert
            state.Entries.ShouldBeEmpty();
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
