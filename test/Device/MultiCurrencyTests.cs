using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.PosForDotNet.Models;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>マルチ通貨(JPY/USD切り替え、フィルタリング、小数の名目値)の検証テスト。</summary>
public class MultiCurrencyTests
{
    private static InternalSimulatorCashChanger CreateDevice()
    {
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Inventory.Clear();

        var jpySettings = new InventorySettings();
        jpySettings.Denominations.Add("B1000", new() { InitialCount = 10 });
        configProvider.Config.Inventory["JPY"] = jpySettings;

        var usdSettings = new InventorySettings();
        usdSettings.Denominations.Add("C0.5", new() { InitialCount = 20 });
        configProvider.Config.Inventory["USD"] = usdSettings;

        // Build inventory explicitly
        var inventory = Inventory.Create();
        foreach (var currencyEntry in configProvider.Config.Inventory)
        {
            foreach (var item in currencyEntry.Value.Denominations)
            {
                if (DenominationKey.TryParse(item.Key, currencyEntry.Key, out var key) && key != null)
                {
                    inventory.SetCount(key, item.Value.InitialCount);
                }
            }
        }

        var hardware = HardwareStatusManager.Create();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, null, null);
        var metadataProvider = CurrencyMetadataProvider.Create(configProvider);
        var monitorsProvider = MonitorsProvider.Create(inventory, configProvider, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var depositController = new DepositController(inventory, hardware);
        var dispenseController = new DispenseController(manager, inventory, configProvider, NullLoggerFactory.Instance, hardware, new Mock<IDeviceSimulator>().Object);

        var deps = new SimulatorDependencies(
            configProvider,
            inventory,
            history,
            manager,
            depositController,
            dispenseController,
            aggregatorProvider,
            hardware);

        var device = new InternalSimulatorCashChanger(deps)
        {
            SkipStateVerification = true
        };
        device.Open();
        device.Claim(0);
        return device;
    }

    /// <summary>サポートされている通貨コードのリストが正しく取得できることを検証する。</summary>
    [Fact]
    public void CurrencyCodeListShouldContainConfiguredCurrencies()
    {
        var device = CreateDevice();
        device.CurrencyCodeList.ShouldContain("JPY");
        device.CurrencyCodeList.ShouldContain("USD");
        device.DepositCodeList.ShouldBe(device.CurrencyCodeList);
    }

    /// <summary>CurrencyCode を切り替えることで、報告される金種情報が正しくフィルタリングされることを検証する。</summary>
    [Fact]
    public void SwitchingCurrencyCodeShouldFilterCashCounts()
    {
        var device = CreateDevice();

        // JPY の場合
        device.CurrencyCode = "JPY";
        var jpyCounts = device.ReadCashCounts();
        jpyCounts.Counts.Length.ShouldBe(1);
        jpyCounts.Counts[0].Type.ShouldBe(CashCountType.Bill);
        jpyCounts.Counts[0].NominalValue.ShouldBe(1000);

        // USD の場合
        device.CurrencyCode = "USD";
        var usdCounts = device.ReadCashCounts();
        usdCounts.Counts.Length.ShouldBe(1);
        usdCounts.Counts[0].Type.ShouldBe(CashCountType.Coin);

        // $0.5 -> 50 (Scaled)
        usdCounts.Counts[0].NominalValue.ShouldBe(50);
    }

    /// <summary>USドルのような小数を含む額面が、正しくスケール調整されて報告されることを検証する。</summary>
    [Fact]
    public void UsdDecimalScalingVerification()
    {
        var device = CreateDevice();
        device.CurrencyCode = "USD";

        // ReadCashCounts で $0.5 が 50 として報告されるか
        var counts = device.ReadCashCounts();
        counts.Counts[0].NominalValue.ShouldBe(50);
    }

    /// <summary>サポートされていない通貨コードを設定しようとした際に例外がスローされることを検証する。</summary>
    [Fact]
    public void SettingInvalidCurrencyCodeShouldThrow()
    {
        var device = CreateDevice();
        Should.Throw<PosControlException>(() => device.CurrencyCode = "EUR")
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }
}
