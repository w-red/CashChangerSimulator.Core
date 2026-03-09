using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Testing;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace CashChangerSimulator.Tests.Device;

public class CustomLogger<T> : ILogger<T>
{
    public List<string> Logs { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Logs.Add(formatter(state, exception));
    }
}

/// <summary>InternalSimulatorCashChanger の UPOS ライフサイクル（Open/Claim/Release/Close）を検証するテストクラス。</summary>
public class UposLifecycleTests
{
    private static (InternalSimulatorCashChanger cc, CustomLogger<SimulatorCashChanger> logger) CreateCashChangerWithLogger()
    {
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Simulation.HotStart = false;
        configProvider.Config.Inventory["JPY"] = new InventorySettings { Denominations = new() { ["C100"] = new() { InitialCount = 50 } } };

        var inv = new Inventory();
        var hw = new HardwareStatusManager();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inv, history, new ChangeCalculator());
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(inv, configProvider, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var depositController = new DepositController(inv, hw);
        var dispenseController = new DispenseController(manager, hw, new Mock<IDeviceSimulator>().Object);

        var deps = new SimulatorDependencies(configProvider, inv, history, manager, depositController, dispenseController, aggregatorProvider, hw);
        
        var logger = new CustomLogger<SimulatorCashChanger>();
        // Note: SimulatorCashChanger uses LogProvider internally, but custom DI would be needed to inject this logger properly.
        // For this test, we might need to rely on the fact that LifecycleManager and Handlers use the logger passed to them.
        
        var changer = new InternalSimulatorCashChanger(deps);
        changer.SkipStateVerification = false;
        return (changer, logger);
    }

    private static InternalSimulatorCashChanger CreateCashChanger() => CreateCashChangerWithLogger().cc;

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

    /// <summary>検証スキップが有効な場合、ベースの Claim を呼ばずに成功することを検証する（NRE回避の確認）。</summary>
    [Fact]
    public void SkipStateVerificationShouldBypassFramework()
    {
        var cc = CreateCashChanger();
        cc.SkipStateVerification = true;
        
        // Open() が UpdateHandler を呼び出すことを検証
        cc.Open();
        
        // SkipVerificationLifecycleHandler が使用されていれば、
        // base.Claim() を呼ばずに成功するはず
        cc.Claim(1000);
        
        cc.Claimed.ShouldBeTrue();
    }

    /// <summary>プロパティ変更時にハンドラーが即座に切り替わることを検証する。</summary>
    [Fact]
    public void HandlerShouldSwitchWhenPropertyChanges()
    {
        var cc = CreateCashChanger();
        cc.Open();
        
        // Skip に切り替え
        cc.SkipStateVerification = true;
        cc.Claim(1000);
        cc.Claimed.ShouldBeTrue();

        // Standard に戻す
        cc.SkipStateVerification = false;
        // この時点で Claim すると実際には base.Claim が呼ばれるが、StandardLifecycleHandler が例外をキャッチする
        cc.Claim(1000);
    }
}
