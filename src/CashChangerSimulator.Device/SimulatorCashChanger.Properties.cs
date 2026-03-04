using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;

namespace CashChangerSimulator.Device;

/// <summary><see cref="SimulatorCashChanger"/> の UPOS プロパティオーバーライドを定義する部分クラス。</summary>
public partial class SimulatorCashChanger
{
    // ========== Status Properties ==========

    /// <summary>デバイスの現在の状態（正常、空、ニアエンプティなど）を取得します。</summary>
    public override CashChangerStatus DeviceStatus => _statusCoordinator?.LastCashChangerStatus ?? CashChangerStatus.OK;
    /// <summary>デバイスの現在の満杯状態（正常、満杯、ニアフルなど）を取得します。</summary>
    public override CashChangerFullStatus FullStatus => _statusCoordinator?.LastFullStatus ?? CashChangerFullStatus.OK;

    // ========== Async Properties ==========

    /// <summary>非同期モードで動作するかどうかを取得または設定します。</summary>
    public override bool AsyncMode { get; set; }
    /// <summary>最後の非同期操作の結果コードを取得します。</summary>
    public override int AsyncResultCode => _asyncResultCode;
    /// <summary>最後の非同期操作の拡張結果コードを取得します。</summary>
    public override int AsyncResultCodeExtended => _asyncResultCodeExtended;

    // ========== Currency Properties ==========

    /// <summary>現在アクティブな通貨コードを取得または設定します。</summary>
    public override string CurrencyCode
    {
        get => _activeCurrencyCode;
        set
        {
            _activeCurrencyCode =
                CurrencyCodeList.Contains(value)
                ? value
                : throw new PosControlException($"Unsupported currency: {value}", ErrorCode.Illegal);
        }
    }
    /// <summary>サポートされている通貨コードの一覧を取得します。</summary>
    public override string[] CurrencyCodeList => [.. _config.Inventory.Keys.OrderBy(c => c)];
    /// <summary>入金可能な通貨コードの一覧を取得します。</summary>
    public override string[] DepositCodeList => CurrencyCodeList;

    /// <summary>アクティブな通貨の現在の現金一覧を取得します。</summary>
    public override CashUnits CurrencyCashList => BuildCashUnits();
    /// <summary>入金可能な現金の一覧を取得します。</summary>
    public override CashUnits DepositCashList => CapDeposit ? BuildCashUnits() : new CashUnits();
    /// <summary>払出可能な現金の一覧を取得します。</summary>
    public override CashUnits ExitCashList => BuildCashUnits();

    // ========== Deposit Properties ==========

    /// <summary>現在投入されている現金の合計金額を取得します。</summary>
    public override int DepositAmount => (int)Math.Round(_depositController.DepositAmount * GetCurrencyFactor());
    /// <summary>現在投入されている現金の金種別枚数を取得します。</summary>
    public override CashCount[] DepositCounts
    {
        get => [.. _depositController.DepositCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .Select(kv => CashCountAdapter.ToCashCount(kv.Key, kv.Value, GetCurrencyFactor()))];
    }
    /// <summary>現在の入金処理の状態を取得します。</summary>
    public override CashDepositStatus DepositStatus => _depositController.DepositStatus;

    // ========== Exit Properties ==========

    /// <summary>現在の排出口インデックスを取得または設定します。</summary>
    public override int CurrentExit { get => 1; set { } }
    /// <summary>デバイスの排出口の総数を取得します。</summary>
    public override int DeviceExits => 1;

    // ========== Capability Properties ==========

    /// <summary>入金機能があるかどうかを取得します。</summary>
    public override bool CapDeposit => true;
    /// <summary>入金データイベントをサポートしているかどうかを取得します。</summary>
    public override bool CapDepositDataEvent => true;
    /// <summary>入金の一時停止をサポートしているかどうかを取得します。</summary>
    public override bool CapPauseDeposit => true;
    /// <summary>入金の返却（払い戻し）をサポートしているかどうかを取得します。</summary>
    public override bool CapRepayDeposit => true;

    /// <summary>不一致の検出をサポートしているかどうかを取得します。</summary>
    public override bool CapDiscrepancy => false;
    /// <summary>満杯センサーをサポートしているかどうかを取得します。</summary>
    public override bool CapFullSensor => true;
    /// <summary>ニアフルセンサーをサポートしているかどうかを取得します。</summary>
    public override bool CapNearFullSensor => true;
    /// <summary>ニアエンプティセンサーをサポートしているかどうかを取得します。</summary>
    public override bool CapNearEmptySensor => true;
    /// <summary>空センサーをサポートしているかどうかを取得します。</summary>
    public override bool CapEmptySensor => true;

    // ========== Private Helpers ==========

    private CashUnits BuildCashUnits()
    {
        var activeUnits = _inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .OrderBy(kv => kv.Key.Value)
            .ToList();

        var coins = activeUnits
            .Where(kv => kv.Key.Type == CashType.Coin)
            .Select(kv => GetNominalValue(kv.Key))
            .ToArray();

        var bills = activeUnits
            .Where(kv => kv.Key.Type == CashType.Bill)
            .Select(kv => GetNominalValue(kv.Key))
            .ToArray();

        return new CashUnits(coins, bills);
    }

    internal int GetNominalValue(DenominationKey key)
    {
        return (int)Math.Round(key.Value * GetCurrencyFactor(key.CurrencyCode));
    }

    internal decimal GetCurrencyFactor(string? currencyCode = null)
    {
        var code = currencyCode ?? _activeCurrencyCode;
        return code switch
        {
            "USD" or "EUR" or "GBP" or "CAD" or "AUD" => 100m,
            _ => 1m
        };
    }
}
