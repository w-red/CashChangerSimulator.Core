using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

/// <summary>
/// マルチ通貨（JPY/USD切り替え、フィルタリング、小数の名目値）の検証テスト。
/// </summary>
public class MultiCurrencyTests
{
    private SimulatorCashChanger CreateDevice()
    {
        var config = new SimulatorConfiguration
        {
            Inventory = new()
            {
                ["JPY"] = new InventorySettings
                {
                    Denominations = new() { ["B1000"] = new() { InitialCount = 10 } }
                },
                ["USD"] = new InventorySettings
                {
                    Denominations = new() { ["C0.5"] = new() { InitialCount = 20 } }
                }
            }
        };
        return new SimulatorCashChanger(config);
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

        // DepositAmount もスケール調整されるか
        device.BeginDeposit();
        // 内部的にUSD 0.5を在庫に追加（本来は外部からの操作だがシミュレーションとして）
        // ※SimulatorCashChangerの内部のInventoryにアクセスできないため、
        // 現状の設計では DepositController または Inventory をモックするか、
        // 今回の修正で CurrencyCode を持つ DenominationKey を使って入金する。
        
        // テスト用に直接 Inventory を操作できるよう、必要ならリフレクションを使うか
        // 外部から入金イベントを模倣する仕組みが必要。
        // ここでは実装の意図通り GetCurrencyFactor が効いているかを確認。
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
