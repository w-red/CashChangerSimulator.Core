using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>SimulatorCashChanger の基本機能（プロパティ、ライフサイクル、診断等）を網羅的に検証するメインテストクラス。</summary>
[Collection("GlobalLock")]
public class SimulatorCashChangerTests
{
    /// <summary>インスタンス生成直後の初期状態（Closed, Unclaimed）を検証します。</summary>
    [Fact]
    public void InitialState_ShouldBeClosed()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        
        // Assert
        changer.State.ShouldBe(ControlState.Closed);
        changer.Claimed.ShouldBeFalse();
        changer.DeviceEnabled.ShouldBeFalse();
    }

    /// <summary>標準的なライフサイクル（Open -> Claim -> Enable -> Release -> Close）の遷移を検証します。</summary>
    [Fact]
    public void Lifecycle_OpenClaimEnable_ShouldTransitionStates()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        changer.SkipStateVerification = false; // Test standard behavior

        // Act & Assert: Open
        changer.Open();
        changer.State.ShouldBe(ControlState.Idle);

        // Act & Assert: Claim
        changer.Claim(1000);
        changer.Claimed.ShouldBeTrue();

        // Act & Assert: Enable
        changer.DeviceEnabled = true;
        changer.DeviceEnabled.ShouldBeTrue();

        // Act & Assert: Disable
        changer.DeviceEnabled = false;
        changer.DeviceEnabled.ShouldBeFalse();

        // Act & Assert: Release
        changer.Release();
        changer.Claimed.ShouldBeFalse();

        // Act & Assert: Close
        changer.Close();
        changer.State.ShouldBe(ControlState.Closed);
    }

    /// <summary>各種 Capability プロパティが設定ファイルの内容を正しく反映していることを検証します。</summary>
    [Fact]
    public void CapProperties_ShouldReflectConfig()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();

        // Assert
        changer.CapDeposit.ShouldBeTrue();
        changer.CapDepositDataEvent.ShouldBeTrue();
        changer.CapPauseDeposit.ShouldBeTrue();
        changer.CapRepayDeposit.ShouldBeTrue();
        changer.CapPurgeCash.ShouldBeTrue();
        changer.CapDiscrepancy.ShouldBeTrue();
        changer.CapFullSensor.ShouldBeTrue();
        changer.CapNearFullSensor.ShouldBeTrue();
        changer.CapNearEmptySensor.ShouldBeTrue();
        changer.CapEmptySensor.ShouldBeTrue();
        changer.CapStatisticsReporting.ShouldBeTrue();
        changer.CapUpdateStatistics.ShouldBeTrue();
        changer.CapRealTimeData.ShouldBeTrue();
    }

    /// <summary>デバイス名、通貨リスト、在庫状況などの各プロパティが内部状態を反映していることを検証します。</summary>
    [Fact]
    public void Properties_ShouldReflectInternalState()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        changer.Open();

        // Assert basic info
        changer.DeviceName.ShouldBe("SimulatorCashChanger");
        changer.DeviceDescription.ShouldBe("Virtual Cash Changer Simulator");

        // Currencies
        changer.CurrencyCode.ShouldBe("JPY");
        changer.CurrencyCodeList.ShouldContain("JPY");
        changer.CurrencyCodeList.ShouldContain("USD");
        changer.DepositCodeList.ShouldContain("JPY");

        // Cash Lists (Verify they don't throw)
        _ = changer.CurrencyCashList;
        _ = changer.DepositCashList;
        _ = changer.ExitCashList;

        // Deposit Info
        changer.DepositAmount.ShouldBe(0);
        changer.DepositCounts.ShouldBeEmpty();
        changer.DepositStatus.ShouldBe(CashDepositStatus.None);

        // Async result (initially success/0)
        changer.AsyncResultCode.ShouldBe(0);
    }

    /// <summary>DirectIO が内部ハンドラへ正しく委譲され、ResultCode が更新されることを検証します。</summary>
    [Fact]
    public void DirectIO_ShouldDelegateToHandler()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;

        // Act
        // Command 0 is not implemented in default, should return failure or empty result
        // But the goal is to see it doesn't throw and sets ResultCode
        var result = changer.DirectIO(999, 0, new object());

        // Assert
        changer.ResultCode.ShouldBe((int)ErrorCode.Success);
    }

    /// <summary>CheckHealth や統計情報の取得・更新・リセットがファサードを介して行われることを検証します。</summary>
    [Fact]
    public void Diagnostics_ShouldDelegateToFacade()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;

        // Act & Assert: CheckHealth
        var health = changer.CheckHealth(HealthCheckLevel.Internal);
        health.ShouldContain("Internal Health Check Report");
        health.ShouldContain("Status: OK");
        changer.CheckHealthText.ShouldBe(health);

        // Act & Assert: Statistics
        var stats = changer.RetrieveStatistics(["*"]);
        stats.ShouldNotBeNull();
        
        changer.UpdateStatistics([new Statistic("Test", 1)]); // Should not throw
        changer.ResetStatistics(["*"]); // Should not throw
    }

    /// <summary>正常時の DeviceStatus および FullStatus を検証します。</summary>
    [Fact]
    public void StatusProperties_ShouldReflectState()
    {
        var changer = new InternalSimulatorCashChanger();
        
        // When closed
        changer.DeviceStatus.ShouldBe(CashChangerStatus.OK);
        changer.FullStatus.ShouldBe(CashChangerFullStatus.OK);

        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;

        // Initial idle state
        changer.DeviceStatus.ShouldBe(CashChangerStatus.OK);
        changer.FullStatus.ShouldBe(CashChangerFullStatus.OK);
    }

    /// <summary>利用可能状態での基本操作（回収、出力クリア、入金セッション）で例外が発生しないことを検証します。</summary>
    [Fact]
    public void CoreOperations_ShouldNotThrowWhenEnabled()
    {
        var changer = new InternalSimulatorCashChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;

        // These should delegate and not throw in simple cases
        changer.PurgeCash();
        changer.ClearOutput();
        
        // Deposit related must be inside a session
        changer.BeginDeposit();
        changer.FixDeposit();
        changer.EndDeposit(CashDepositAction.NoChange);

        changer.BeginDeposit();
        changer.RepayDeposit();
    }

    /// <summary>ResultCode および ResultCodeExtended が外部から設定・取得可能であることを検証します。</summary>
    [Fact]
    public void ResultCode_ShouldBeSettable()
    {
        var changer = new InternalSimulatorCashChanger();
        changer.ResultCode = (int)ErrorCode.Illegal;
        changer.ResultCode.ShouldBe((int)ErrorCode.Illegal);
        
        changer.ResultCodeExtended = 999;
        changer.ResultCodeExtended.ShouldBe(999);
    }

    /// <summary>回収口（Exit）に関連するプロパティの動作を検証します。</summary>
    [Fact]
    public void Exits_ShouldReflectConfig()
    {
        var changer = new InternalSimulatorCashChanger();
        changer.DeviceExits.ShouldBe(1);
        changer.CurrentExit = 1;
        changer.CurrentExit.ShouldBe(1);
    }

    /// <summary>RealTimeDataEnabled プロパティが設定可能であることを検証します。</summary>
    [Fact]
    public void RealTimeDataEnabled_ShouldBeSettable()
    {
        var changer = new InternalSimulatorCashChanger();
        changer.RealTimeDataEnabled = true;
        changer.RealTimeDataEnabled.ShouldBeTrue();
    }
}
