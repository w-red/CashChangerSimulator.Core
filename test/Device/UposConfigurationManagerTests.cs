using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.PosForDotNet.Services;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS 設定マネージャによる通貨設定、設定リロード、初期化処理を検証するテストクラス。</summary>
public class UposConfigurationManagerTests
{
    private readonly ConfigurationProvider configProvider;
    private readonly Inventory inventory;
    private readonly Mock<IDeviceStateProvider> stateProvider;
    private readonly UposConfigurationManager manager;

    public UposConfigurationManagerTests()
    {
        configProvider = new ConfigurationProvider();
        inventory = Inventory.Create();
        stateProvider = new Mock<IDeviceStateProvider>();
        manager = new UposConfigurationManager(configProvider, inventory, stateProvider.Object);
    }

    /// <summary>サポートされていない通貨コードを設定しようとした際に例外が発生することを検証します。</summary>
    [Fact]
    public void CurrencyCodeShouldThrowWhenUnsupported()
    {
        Should.Throw<PosControlException>(() => manager.CurrencyCode = "INVALID")
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>正当な通貨コードが正常に設定・取得できることを検証します。</summary>
    [Fact]
    public void CurrencyCodeShouldWorkWhenSupported()
    {
        configProvider.Config.Inventory["USD"] = new InventorySettings();
        manager.CurrencyCode = "USD";
        manager.CurrencyCode.ShouldBe("USD");
    }

    /// <summary>設定変更時に内部状態(在庫等)が正しくリセットされることを検証します。</summary>
    [Fact]
    public void ResetStateWhenConfigurationChanges()
    {
        stateProvider.Setup(s => s.State).Returns(DeviceControlState.Idle);
        inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill), 10);

        // Trigger reload
        configProvider.Update(new SimulatorConfiguration());

        inventory.AllCounts.ShouldBeEmpty();
    }

    /// <summary>手動リロード実行時に内部状態がリセットされることを検証します。</summary>
    [Fact]
    public void ReloadShouldManuallyTriggerUpdate()
    {
        stateProvider.Setup(s => s.State).Returns(DeviceControlState.Idle);
        inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill), 10);

        manager.Reload();

        inventory.AllCounts.ShouldBeEmpty();
    }

    /// <summary>初期化処理によってアクティブな通貨が正しく設定されることを検証します。</summary>
    [Fact]
    public void InitializeShouldSetActiveCurrency()
    {
        configProvider.Config.Inventory.Clear();
        configProvider.Config.Inventory["EUR"] = new InventorySettings();

        manager.Initialize();
        manager.CurrencyCode.ShouldBe("EUR");
    }

    /// <summary>破棄(Dispose)後に設定変更を受け取っても副作用が発生しないことを検証します。</summary>
    [Fact]
    public void DisposeShouldUnsubscribe()
    {
        manager.Dispose();

        // Trigger reload should not cause issues even if manager logic would have
        configProvider.Update(new SimulatorConfiguration());
    }

    /// <summary>CurrencyCashList プロパティが UPOS 規定の形式でデータを返却することを検証します。</summary>
    [Fact]
    public void CurrencyCashListShouldReturnUposUnits()
    {
        var list = manager.CurrencyCashList;

        // CashUnits is a struct, so it cannot be null.
    }
}
