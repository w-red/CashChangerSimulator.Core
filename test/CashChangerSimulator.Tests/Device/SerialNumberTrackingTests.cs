using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>入金確定（FixDeposit）時の記番号（シリアルナンバー）追跡機能を検証するテストクラス。</summary>
public class SerialNumberTrackingTests
{
    private static (InternalSimulatorCashChanger changer, DepositController controller) CreateChanger()
    {
        var changer = new InternalSimulatorCashChanger
        {
            // SkipStateVerification allows calling BeginDeposit etc without full OPOS lifecycle
        };
        changer.SkipStateVerification = true;
        changer.Open();
        changer.Claim(0);
        changer.Claim(0);

        var controller = changer.DepositController;

        return (changer, controller);
    }

    /// <summary>DirectIO(GetVersion) が正しいバージョン文字列を返却することを検証します。</summary>
    [Fact]
    public void DirectIOGetVersionShouldWorkWithConstant()
    {
        // Arrange
        var (changer, _) = CreateChanger();

        // Act
        var result = changer.DirectIO(DirectIOCommands.GetVersion, 0, "");

        // Assert
        result.Object.ShouldNotBeNull();
        result.Object.ToString()!.ShouldContain("InternalSimulatorCashChanger");
    }

    /// <summary>初期状態で DirectIO(GetDepositedSerials) が空の結果を返却することを検証します。</summary>
    [Fact]
    public void DirectIOGetDepositedSerialsShouldReturnEmptyInitially()
    {
        // Arrange
        var (changer, _) = CreateChanger();

        // Act
        var result = changer.DirectIO(DirectIOCommands.GetDepositedSerials, 0, "");

        // Assert
        result.Object.ShouldNotBeNull();
        result.Object.ToString()!.ShouldBe("");
    }

    /// <summary>FixDeposit 実行後に DirectIO(GetDepositedSerials) から記番号が取得できることを検証します。</summary>
    [Fact]
    public void DirectIOGetDepositedSerialsShouldReturnSerialsAfterDepositFix()
    {
        // Arrange
        var (changer, controller) = CreateChanger();
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        changer.BeginDeposit();
        controller.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key1000, 2 } });

        // Serials are captured but not and "last" yet until FixDeposit
        controller.LastDepositedSerials.Count.ShouldBe(0);

        // Act
        changer.FixDeposit();

        // Assert
        var result = changer.DirectIO(DirectIOCommands.GetDepositedSerials, 0, "");
        result.Object.ShouldNotBeNull();
        var serials = result.Object.ToString()!.Split(',');

        serials?.Length.ShouldBe(2);
        serials?[0].ShouldStartWith("S1000-");
        serials?[1].ShouldStartWith("S1000-");
        serials?[0].ShouldNotBe(serials?[1]);
    }

    /// <summary>EndDeposit 実行後も直近の記番号データが保持されていることを検証します。</summary>
    [Fact]
    public void DirectIOGetDepositedSerialsShouldPersistAfterEndDeposit()
    {
        // Arrange
        var (changer, controller) = CreateChanger();
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        changer.BeginDeposit();
        controller.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key1000, 1 } });
        changer.FixDeposit();

        var resultBeforeHeader = changer.DirectIO(DirectIOCommands.GetDepositedSerials, 0, "");
        string serialBefore = resultBeforeHeader.Object?.ToString() ?? "";

        // Act
        changer.EndDeposit(CashDepositAction.NoChange);

        // Assert
        var resultAfter = changer.DirectIO(DirectIOCommands.GetDepositedSerials, 0, "");
        resultAfter.Object.ShouldNotBeNull();
        resultAfter.Object.ToString()!.ShouldBe(serialBefore);
    }
}
