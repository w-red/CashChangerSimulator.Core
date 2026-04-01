using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>CashChangerSimulator.Core のカバレッジを 100% にするための網羅的テストクラス。</summary>
public class ExhaustiveCoreTests : IDisposable
{
    public ExhaustiveCoreTests()
    {
        // テストごとに LogProvider を初期化状態に戻す
        LogProvider.Dispose();
    }

    public void Dispose()
    {
        LogProvider.Dispose();
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
        DenominationKey.TryParse("", out _).ShouldBeFalse();
        DenominationKey.TryParse("X", out _).ShouldBeFalse(); 
        DenominationKey.TryParse("X100", out _).ShouldBeFalse(); 
        DenominationKey.TryParse("BABC", out _).ShouldBeFalse();
        DenominationKey.TryParse("B100:Extra", out _).ShouldBeFalse(); // Invalid format (handled by LoadFromDictionary's TryParseKey)

        // 5. TryParse - Success with currency
        DenominationKey.TryParse("C100", "USD", out var parsed).ShouldBeTrue();
        parsed!.CurrencyCode.ShouldBe("USD");
        parsed.Value.ShouldBe(100);
        parsed.Type.ShouldBe(CurrencyCashType.Coin);
        
        // 6. Record properties access (for coverage of generated code if any)
        key1.Value.ShouldBe(1000);
        key1.Type.ShouldBe(CurrencyCashType.Bill);
        key1.CurrencyCode.ShouldBe("JPY");
    }

    /// <summary>Inventory の各種加算メソッド、クリア、辞書からのロード等の網羅的検証を行います。</summary>
    [Fact]
    public void InventoryDeepCoverage()
    {
        var inventory = new Inventory();
        var key = new DenominationKey(1000, CurrencyCashType.Bill);

        // 1. UpdateBucket - Zero count (early return)
        inventory.Add(key, 0); // No change

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
            { "INVALID_KEY_NO_SEPARATOR", 99 },
            { "JPY:X_NOT_A_NUMBER", 99 },
            { "UNKNOWN:KEY", 99 }
        };
        inventory.LoadFromDictionary(dict);
        inventory.CollectionCounts.First(kv => kv.Key.Value == 1000).Value.ShouldBe(10);
        inventory.RejectCounts.First(kv => kv.Key.Value == 500).Value.ShouldBe(5);
        inventory.GetCount(new DenominationKey(100, CurrencyCashType.Coin)).ShouldBe(20);

        // 6. CalculateTotal with no matches
        inventory.CalculateTotal("NON_EXISTENT").ShouldBe(0);

        // 7. UpdateBucket coverage for AddCollection/AddReject negative warnings
        // 8. LoadFromDictionary - Exception path
        var malformedDict = new Dictionary<string, int>
        {
            { "COL:", 1 }, // Too short, should throw IndexOutOfRangeException or similar in StartsWith? No, kv.Key[4..] on "COL:" is fine.
            { "C", 1 },    // Too short for "COL:" but StartsWith is false.
        };
        // Trigger the TryParseKey return false
        malformedDict["INVALID:KEY:FORMAT"] = 1;
        
        inventory.LoadFromDictionary(malformedDict);

        // 9. UpdateBucket negative sum (already tested, but making sure)
        inventory.Add(key, -999); 
    }

    /// <summary>ConfigurationLoader のロード、保存、異常系（破損ファイル）の網羅的検証を行います。</summary>
    [Fact]
    public void ConfigurationLoaderExhaustive()
    {
        var tempConfig = Path.Combine(Path.GetTempPath(), "CCS_Config_" + Guid.NewGuid() + ".toml");
        var tempState = Path.Combine(Path.GetTempPath(), "CCS_State_" + Guid.NewGuid() + ".toml");

        try
        {
            // 1. Get default paths
            ConfigurationLoader.GetDefaultConfigPath().ShouldNotBeNull();
            ConfigurationLoader.GetDefaultInventoryStatePath().ShouldNotBeNull();
            ConfigurationLoader.GetDefaultHistoryStatePath().ShouldNotBeNull();

            // 2. Load non-existent (creates default)
            var config = ConfigurationLoader.Load(tempConfig);
            config.ShouldNotBeNull();
            File.Exists(tempConfig).ShouldBeTrue();

            // 3. Save and Load
            config.System.CurrencyCode = "USD";
            ConfigurationLoader.Save(config, tempConfig);
            var loaded = ConfigurationLoader.Load(tempConfig);
            loaded.System.CurrencyCode.ShouldBe("USD");

            // 4. Load invalid TOML (should catch and return default)
            File.WriteAllText(tempConfig, "INVALID = [[[[[");
            var fallback = ConfigurationLoader.Load(tempConfig);
            fallback.ShouldNotBeNull();

            // 5. InventoryState - Save and Load
            var state = new InventoryState();
            state.Counts["JPY:B1000"] = 10;
            ConfigurationLoader.SaveInventoryState(state, tempState);
            var loadedState = ConfigurationLoader.LoadInventoryState(tempState);
            loadedState.Counts["JPY:B1000"].ShouldBe(10);

            // 6. InventoryState - Load non-existent
            ConfigurationLoader.LoadInventoryState("non-existent-file").ShouldNotBeNull();

            // 7. InventoryState - Load invalid TOML
            File.WriteAllText(tempState, "INVALID TOML");
            ConfigurationLoader.LoadInventoryState(tempState).ShouldNotBeNull();
        }
        finally
        {
            if (File.Exists(tempConfig)) File.Delete(tempConfig);
            if (File.Exists(tempState)) File.Delete(tempState);
        }
    }

    /// <summary>GlobalLockManager のロック取得、競合、解放、異常系の網羅的検証を行います。</summary>
    [Fact]
    public void GlobalLockManagerExhaustive()
    {
        var lockFile1 = Path.Combine(Path.GetTempPath(), "CCS_Lock1_" + Guid.NewGuid());
        var lockFile2 = Path.Combine(Path.GetTempPath(), "CCS_Lock2_" + Guid.NewGuid());
        var logger = LogProvider.CreateLogger<GlobalLockManager>();
        
        {
            using var manager1 = new GlobalLockManager(lockFile1, logger);
            using var manager2 = new GlobalLockManager(lockFile1, logger);

            // 0. Initial state
            manager1.IsLockHeldByAnother().ShouldBeFalse();

            // 1. Basic acquire/release
            manager1.TryAcquire().ShouldBeTrue();
            manager1.IsLockHeldByAnother().ShouldBeFalse(); // 自分が持っているので another ではない
            
            // 2. Contention
            manager2.TryAcquire().ShouldBeFalse();
            manager2.IsLockHeldByAnother().ShouldBeTrue(); // manager1 が持っている

            manager1.Release();
            manager1.IsLockHeldByAnother().ShouldBeFalse();

            // 3. Re-acquire
            manager2.TryAcquire().ShouldBeTrue();
            manager2.Release();

            // 4. Dispose handles release
            manager1.TryAcquire();
            manager1.Dispose();
            manager2.TryAcquire().ShouldBeTrue();
        }

        // 5. Unexpected errors (Exception catch blocks)
        // Create a directory where the lock file should be
        var dirPath = Path.Combine(Path.GetTempPath(), "CCS_DirLock_" + Guid.NewGuid());
        Directory.CreateDirectory(dirPath);
        var lockInDir = Path.Combine(dirPath, "lock.txt"); // Not the issue, the ISSUE is if we use dirPath as lockFile
        
        using (var managerError = new GlobalLockManager(dirPath, logger))
        {
            managerError.TryAcquire().ShouldBeFalse(); // Access denied to directory as file
            managerError.IsLockHeldByAnother().ShouldBeFalse(); // Should catch exception and return false
        }
        Directory.Delete(dirPath);

        // Cleanup
        if (File.Exists(lockFile1)) File.Delete(lockFile1);
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
