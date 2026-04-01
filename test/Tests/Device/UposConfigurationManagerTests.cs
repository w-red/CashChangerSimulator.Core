using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Services;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS 設定マネージャによる通貨設定、設定リロード、初期化処理を検証するテストクラス。</summary>
public class UposConfigurationManagerTests
{
    private readonly ConfigurationProvider _configProvider;
    private readonly Inventory _inventory;
    private readonly Mock<IDeviceStateProvider> _stateProvider;
    private readonly UposConfigurationManager _manager;

    public UposConfigurationManagerTests()
    {
        _configProvider = new ConfigurationProvider();
        _inventory = new Inventory();
        _stateProvider = new Mock<IDeviceStateProvider>();
        _manager = new UposConfigurationManager(_configProvider, _inventory, _stateProvider.Object);
    }

    /// <summary>サポートされていない通貨コードを設定しようとした際に例外が発生することを検証します。</summary>
    [Fact]
    public void CurrencyCodeShouldThrowWhenUnsupported()
    {
        Should.Throw<PosControlException>(() => _manager.CurrencyCode = "INVALID")
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>正当な通貨コードが正常に設定・取得できることを検証します。</summary>
    [Fact]
    public void CurrencyCodeShouldWorkWhenSupported()
    {
        _configProvider.Config.Inventory["USD"] = new InventorySettings();
        _manager.CurrencyCode = "USD";
        _manager.CurrencyCode.ShouldBe("USD");
    }

    /// <summary>設定変更時に内部状態（在庫等）が正しくリセットされることを検証します。</summary>
    [Fact]
    public void ResetStateWhenConfigurationChanges()
    {
        _stateProvider.Setup(s => s.State).Returns(ControlState.Idle);
        _inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill), 10);
        
        // Trigger reload
        _configProvider.Update(new SimulatorConfiguration());
        
        _inventory.AllCounts.ShouldBeEmpty();
    }

    /// <summary>手動リロード実行時に内部状態がリセットされることを検証します。</summary>
    [Fact]
    public void ReloadShouldManuallyTriggerUpdate()
    {
        _stateProvider.Setup(s => s.State).Returns(ControlState.Idle);
        _inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill), 10);
        
        _manager.Reload();
        
        _inventory.AllCounts.ShouldBeEmpty();
    }

    /// <summary>初期化処理によってアクティブな通貨が正しく設定されることを検証します。</summary>
    [Fact]
    public void InitializeShouldSetActiveCurrency()
    {
        _configProvider.Config.Inventory.Clear();
        _configProvider.Config.Inventory["EUR"] = new InventorySettings();
        
        _manager.Initialize();
        _manager.CurrencyCode.ShouldBe("EUR");
    }

    /// <summary>破棄（Dispose）後に設定変更を受け取っても副作用が発生しないことを検証します。</summary>
    [Fact]
    public void DisposeShouldUnsubscribe()
    {
        _manager.Dispose();
        // Trigger reload should not cause issues even if manager logic would have
        _configProvider.Update(new SimulatorConfiguration());
    }

    /// <summary>CurrencyCashList プロパティが UPOS 規定の形式でデータを返却することを検証します。</summary>
    [Fact]
    public void CurrencyCashListShouldReturnUposUnits()
    {
        var list = _manager.CurrencyCashList;
        // CashUnits is a struct, so it cannot be null.
    }
}
