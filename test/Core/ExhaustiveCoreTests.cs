using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Core;

/// <summary>CashChangerSimulator.Core のカバレッジを 100% にするための網羅的テストクラス。</summary>
public class ExhaustiveCoreTests : IDisposable
{
    /// <summary>ExhaustiveCoreTests の初期化。LogProvider をリセットします。</summary>
    public ExhaustiveCoreTests()
    {
        LogProvider.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        LogProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>LogProvider の各設定パス（コンソール、ファイル、ログレベル）を網羅的に検証します。</summary>
    [Fact]
    public void LogProviderShouldCoverageAllPaths()
    {
        // 1. デフォルト状態 (NullLoggerFactory)
        LogProvider.Factory.ShouldNotBeNull();
        LogProvider.CreateLogger<ExhaustiveCoreTests>().ShouldNotBeNull();

        // 2. Initialize - Console Enable
        var settings = new LoggingSettings { LogLevel = "Debug", EnableConsole = true, EnableFile = false };
        LogProvider.Initialize(settings);
        LogProvider.CreateLogger<ExhaustiveCoreTests>().LogInformation("Test console log");

        // 3. SetLogLevel - Invalid level (should fallback to Information)
        LogProvider.SetLogLevel("INVALID_LEVEL");

        // 4. Initialize - File Enable (including directory creation)
        var logDir = Path.Combine(Path.GetTempPath(), "CCS_Logs_" + Guid.NewGuid());
        var fileSettings = new LoggingSettings
        {
            LogLevel = "Information",
            EnableConsole = false,
            EnableFile = true,
            LogDirectory = logDir,
            LogFileName = "test.log"
        };
        LogProvider.Initialize(fileSettings);
        LogProvider.CreateLogger<ExhaustiveCoreTests>().LogInformation("Test file log");

        File.Exists(Path.Combine(logDir, "test.log")).ShouldBeTrue();

        // 5. Dispose
        LogProvider.Dispose();
    }

    /// <summary>DenominationKey の等価性、ハッシュ、文字列変換、パースの網羅的検証を行います。</summary>
    [Fact]
    public void DenominationKeyExhaustiveTests()
    {
        var key1 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // 1. Equality and Hashing
        var key2 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var key3 = new DenominationKey(500, CurrencyCashType.Coin, "JPY");

        key1.ShouldBe(key2);
        key1.GetHashCode().ShouldBe(key2.GetHashCode());
        key1.ShouldNotBe(key3);

        // 2. PrefixChar
        key1.PrefixChar.ShouldBe('B');
        key3.PrefixChar.ShouldBe('C');

        // 3. ToDenominationString
        key1.ToDenominationString().ShouldBe("B1000");
        key3.ToDenominationString().ShouldBe("C500");

        // 4. TryParse - Failure cases
        DenominationKey.TryParse(null!, out _).ShouldBeFalse();
        DenominationKey.TryParse(string.Empty, out _).ShouldBeFalse();
        DenominationKey.TryParse("X", out _).ShouldBeFalse();
        DenominationKey.TryParse("X100", out _).ShouldBeFalse();
        DenominationKey.TryParse("BABC", out _).ShouldBeFalse();
        DenominationKey.TryParse("B100:Extra", out _).ShouldBeFalse();

        // 5. TryParse - Success with currency
        DenominationKey.TryParse("C100", "USD", out var parsed).ShouldBeTrue();
        parsed!.CurrencyCode.ShouldBe("USD");
        parsed.Value.ShouldBe(100);
        parsed.Type.ShouldBe(CurrencyCashType.Coin);

        // 6. Record properties access
        key1.Value.ShouldBe(1000);
        key1.Type.ShouldBe(CurrencyCashType.Bill);
        key1.CurrencyCode.ShouldBe("JPY");
    }

    /// <summary>Inventory の各種加算メソッド、クリア、辞書からのロード等の網羅的検証を行います。</summary>
    [Fact]
    public void InventoryDeepCoverage()
    {
        var inventory = Inventory.Create();
        var key = new DenominationKey(1000, CurrencyCashType.Bill);

        // 1. UpdateBucket - Zero count (early return)
        inventory.Add(key, 0);

        // 2. Add with negative resulting in warning log
        inventory.Add(key, -100);
        inventory.GetCount(key).ShouldBe(0);

        // 3. GetTotalCount
        inventory.Add(key, 1);
        inventory.AddCollection(key, 1);
        inventory.AddReject(key, 1);
        inventory.AddEscrow(key, 1);
        inventory.GetTotalCount(key).ShouldBe(4);

        // 4. Clear
        inventory.Clear();
        inventory.CalculateTotal().ShouldBe(0);

        // 5. LoadFromDictionary - Branch coverage (COL:, REJ:, normal)
        var dict = new Dictionary<string, int>
        {
            { "COL:JPY:B1000", 10 },
            { "REJ:JPY:B500", 5 },
            { "JPY:C100", 20 },
            { "INVALID_KEY", 99 }
        };
        inventory.LoadFromDictionary(dict);
        inventory.CollectionCounts.First(kv => kv.Key.Value == 1000).Value.ShouldBe(10);
        inventory.RejectCounts.First(kv => kv.Key.Value == 500).Value.ShouldBe(5);
        inventory.GetCount(new DenominationKey(100, CurrencyCashType.Coin)).ShouldBe(20);

        // 6. CalculateTotal with no matches
        inventory.CalculateTotal("NON_EXISTENT").ShouldBe(0);
    }

    /// <summary>GlobalLockManager の再帰的ロック取得、競合、解放、異常系の網羅的検証を行います。</summary>
    [Fact]
    public void GlobalLockManagerExhaustive()
    {
        var lockFile = Path.Combine(Path.GetTempPath(), "CCS_ExLock_" + Guid.NewGuid());
        var logger = LogProvider.CreateLogger<GlobalLockManager>();

        using var manager1 = new GlobalLockManager(lockFile, logger);
        using var manager2 = new GlobalLockManager(lockFile, logger);

        // 1. Initial state
        manager1.IsLockHeldByAnother().ShouldBeFalse();

        // 2. Recursive acquire (Already held by self)
        manager1.TryAcquire().ShouldBeTrue();
        manager1.TryAcquire().ShouldBeTrue(); 
        manager1.IsLockHeldByAnother().ShouldBeFalse();

        // 3. Contention from another manager
        manager2.TryAcquire().ShouldBeFalse();
        manager2.IsLockHeldByAnother().ShouldBeTrue();

        // 4. Release and re-acquire
        manager1.Release();
        manager1.IsLockHeldByAnother().ShouldBeFalse();
        manager2.TryAcquire().ShouldBeTrue();
        manager2.Release();

        // 5. Exception handling (Accessing a directory as a file)
        var dirPath = Path.Combine(Path.GetTempPath(), "CCS_DirLock_" + Guid.NewGuid());
        Directory.CreateDirectory(dirPath);
        using (var dirManager = new GlobalLockManager(dirPath, logger))
        {
            dirManager.TryAcquire().ShouldBeFalse();
            dirManager.IsLockHeldByAnother().ShouldBeFalse();
        }
        Directory.Delete(dirPath);
    }

    /// <summary>TransactionEntry のプロパティ保持を検証します。</summary>
    [Fact]
    public void TransactionEntryCoverage()
    {
        var denoms = new Dictionary<DenominationKey, int> { { new DenominationKey(1000, CurrencyCashType.Bill), 1 } };
        var entry = new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 1000, denoms);

        entry.Timestamp.ShouldNotBe(default);
        entry.Type.ShouldBe(TransactionType.Deposit);
        entry.Amount.ShouldBe(1000);
        entry.Counts.Count.ShouldBe(1);
    }
}
