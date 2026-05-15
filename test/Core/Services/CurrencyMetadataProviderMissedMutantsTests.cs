using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Core.Services;

public class CurrencyMetadataProviderMissedMutantsTests
{
    [Fact]
    public void ShouldCoverDefaultStrings()
    {
        // 1. Create a config with null CurrencyCode and CultureCode to cover L74, L78, L222
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = null!;
        config.System.CultureCode = null!;
        
        var configProvider = new ConfigurationProvider(false);
        configProvider.Update(config);
        
        var provider = CurrencyMetadataProvider.Create(configProvider);
        
        // JPY 1000 has a DisplayName in default config, so we clear it to reach L194
        config.Inventory["JPY"].Denominations["B1000"].DisplayName = null;
        configProvider.Update(config);
        
        provider.CurrencyCode.ShouldBe("JPY"); // Covered L74, L221
        // CultureCode default "en-US" is internal, but we can verify it via GetDenominationName
        // If it's "en-US", it's not Japanese.
        var jpyKey = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        provider.GetDenominationName(jpyKey).ShouldBe("1,000 Yen"); // JPY + non-Japanese culture = "val Yen"
        
        // 3. Cover L179 ("N0")
        // We need a non-JPY currency (to reach L199) where setting.FormatSpecifier is null and Value is integer
        var usdKey = new DenominationKey(1, CurrencyCashType.Coin, "USD");
        config.System.CurrencyCode = "USD";
        config.Inventory["USD"] = new InventorySettings();
        // Add a denomination without FormatSpecifier
        config.Inventory["USD"].Denominations[usdKey.ToDenominationString()] = new DenominationSettings 
        { 
            FormatSpecifier = null // This covers the ?? branch in L179
        };
        configProvider.Update(config);
        
        // USD uses prefix, so we check prefix too
        provider.GetDenominationName(usdKey).ShouldBe("1 Coin"); // prefix is empty by default, valStr is "1" (N0), typeName is "Coin"
    }
}
