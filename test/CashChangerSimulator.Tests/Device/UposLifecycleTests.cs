using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>InternalSimulatorCashChanger の UPOS ライフサイクル（Open/Claim/Release/Close）を検証するテストクラス。</summary>
public class UposLifecycleTests
{
    private static InternalSimulatorCashChanger CreateCashChanger()
    {
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Simulation.HotStart = false; // ColdStart is now the baseline
        configProvider.Config.Inventory["JPY"] = new InventorySettings
        {
            Denominations = new()
            {
                ["C100"] = new() { InitialCount = 50 }
            }
        };

        var inv = new Inventory();
        var hw = new HardwareStatusManager();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inv, history, new ChangeCalculator());
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(inv, configProvider, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var depositController = new DepositController(inv, hw);
        var dispenseController = new DispenseController(manager, hw, new Mock<IDeviceSimulator>().Object);

        var deps = new SimulatorDependencies(
            configProvider,
            inv,
            history,
            manager,
            depositController,
            dispenseController,
            aggregatorProvider,
            hw);

        return new InternalSimulatorCashChanger(deps)
        {
            SkipStateVerification = false
        };
    }

    /// <summary>占有されていない状態で DispenseChange を呼び出すと例外がスローされることを検証する。</summary>
    [Fact]
    public void DispenseChangeShouldThrowWhenNotClaimed()
    {
        var cc = CreateCashChanger();
        Should.Throw<PosControlException>(() => cc.DispenseChange(100));
    }

    /// <summary>占有されていない状態で BeginDeposit を呼び出すと例外がスローされることを検証する。</summary>
    [Fact]
    public void BeginDepositShouldThrowWhenNotClaimed()
    {
        var cc = CreateCashChanger();
        var ex = Should.Throw<PosControlException>(() => cc.BeginDeposit());
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>占有されていない状態で ReadCashCounts を呼び出すと例外がスローされることを検証する。</summary>
    [Fact]
    public void ReadCashCountsShouldThrowWhenNotClaimed()
    {
        var cc = CreateCashChanger();
        var ex = Should.Throw<PosControlException>(() => cc.ReadCashCounts());
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>デバイスがオープンされる前に占有（Claim）を試みると例外がスローされることを検証する。</summary>
    [Fact]
    public void ClaimBeforeOpenShouldSucceedInWaitState()
    {
        var cc = CreateCashChanger();
        var ex = Should.Throw<PosControlException>(() => cc.Claim(1000));
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>正常な Open/Claim シーケンスがエラーなく完了することを検証する。</summary>
    [Fact]
    public void SuccessfulLifecycle()
    {
        var cc = CreateCashChanger();
        cc.Open();
        cc.Claim(1000);
    }

    /// <summary>健康状態確認（CheckHealth）が常に OK を返すことを検証する。</summary>
    [Fact]
    public void CheckHealthShouldReturnOk()
    {
        var cc = CreateCashChanger();
        cc.CheckHealth(HealthCheckLevel.Internal).ShouldContain("OK");
    }
}
