using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Tests.UI;

/// <summary>
/// Common constants and shared data for UI-related unit tests to eliminate magic numbers.
/// </summary>
public static class TestConstants
{
    public const string DefaultCurrency = "JPY";

    // Denomination Keys
    public static readonly DenominationKey Key100 = new(100, CurrencyCashType.Coin, DefaultCurrency);
    public static readonly DenominationKey Key1000 = new(1000, CurrencyCashType.Bill, DefaultCurrency);
    public static readonly DenominationKey Key5000 = new(5000, CurrencyCashType.Bill, DefaultCurrency);
    public static readonly DenominationKey Key10000 = new(10000, CurrencyCashType.Bill, DefaultCurrency);

    // Initial Counts (Config values)
    public const int ConfigCount100 = 50;
    public const int ConfigCount1000 = 20;

    // Current Counts (Starting values for tests)
    public const int StartCount100 = 10;
    public const int StartCount1000 = 5;
}
