using Microsoft.PointOfService;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Device;

/// <summary>
/// UPOS v1.5+ の Deposit シーケンスを管理するコントローラー。
/// beginDeposit → fixDeposit → endDeposit の状態遷移とバリデーションを担う。
/// </summary>
public class DepositController
{
    private readonly Inventory _inventory;
    private readonly CashChangerManager _manager;
    
    private decimal _depositAmount;
    private readonly Dictionary<DenominationKey, int> _depositCounts = [];
    private CashDepositStatus _depositStatus = CashDepositStatus.None;
    private bool _depositPaused;
    private bool _depositFixed;

    public DepositController(Inventory inventory, CashChangerManager manager)
    {
        _inventory = inventory;
        _manager = manager;
    }

    // ========== Properties ==========

    /// <summary>入金合計額。</summary>
    public decimal DepositAmount => _depositAmount;

    /// <summary>金種ごとの入金枚数。</summary>
    public IReadOnlyDictionary<DenominationKey, int> DepositCounts => _depositCounts;

    /// <summary>入金ステータス。</summary>
    public CashDepositStatus DepositStatus => _depositStatus;

    /// <summary>入金受付中かどうか（払出ガード判定用）。</summary>
    public bool IsDepositInProgress => 
        _depositStatus is CashDepositStatus.Start or CashDepositStatus.Count;

    /// <summary>一時停止中かどうか。</summary>
    public bool IsPaused => _depositPaused;

    // ========== Methods ==========

    /// <summary>
    /// UPOS 8.5.2: 入金受付を開始する。
    /// DepositCounts と DepositAmount を 0 に初期化。
    /// </summary>
    public void BeginDeposit()
    {
        _depositAmount = 0m;
        _depositCounts.Clear();
        _depositStatus = CashDepositStatus.Start;
        _depositPaused = false;
        _depositFixed = false;
        _depositStatus = CashDepositStatus.Count;
    }

    /// <summary>
    /// UPOS 8.5.6: 入金を確定する。
    /// beginDeposit が先に呼ばれていない場合は E_ILLEGAL。
    /// </summary>
    public void FixDeposit()
    {
        if (_depositStatus is not (CashDepositStatus.Start or CashDepositStatus.Count))
        {
            throw new PosControlException(
                "The call sequence is invalid. beginDeposit must be called before fixDeposit.",
                ErrorCode.Illegal);
        }
        _depositFixed = true;
    }

    /// <summary>
    /// UPOS 8.5.5: 入金受付を完了する。
    /// fixDeposit が先に呼ばれていない場合は E_ILLEGAL。
    /// </summary>
    public void EndDeposit(CashDepositAction action)
    {
        if (!_depositFixed)
        {
            throw new PosControlException(
                "The call sequence is invalid. fixDeposit must be called before endDeposit.",
                ErrorCode.Illegal);
        }

        switch (action)
        {
            case CashDepositAction.Change:
                _depositStatus = CashDepositStatus.End;
                if (_depositAmount > 0)
                {
                    _manager.Dispense(_depositAmount);
                }
                break;

            case CashDepositAction.Repay:
                foreach (var kv in _depositCounts)
                {
                    _inventory.Add(kv.Key, -kv.Value);
                }
                _depositStatus = CashDepositStatus.End;
                break;

            default: // NoChange
                _depositStatus = CashDepositStatus.End;
                break;
        }
        _depositPaused = false;
        _depositFixed = false;
    }

    /// <summary>
    /// UPOS 8.5.7: 入金一時停止 / 再開。
    /// すでにその状態である場合は E_ILLEGAL。
    /// </summary>
    public void PauseDeposit(CashDepositPause control)
    {
        if (_depositStatus is not (CashDepositStatus.Start or CashDepositStatus.Count))
        {
            throw new PosControlException("beginDeposit must be called before pauseDeposit.", ErrorCode.Illegal);
        }

        if (control == CashDepositPause.Pause)
        {
            if (_depositPaused) throw new PosControlException("Already paused.", ErrorCode.Illegal);
            _depositPaused = true;
        }
        else
        {
            if (!_depositPaused) throw new PosControlException("Not paused.", ErrorCode.Illegal);
            _depositPaused = false;
        }
    }

    /// <summary>
    /// 入金中に金種が追加されたときに呼ばれるトラッキングメソッド。
    /// </summary>
    public void TrackDeposit(DenominationKey key)
    {
        if (_depositStatus != CashDepositStatus.Count) return;
        if (_depositPaused) return;

        _depositAmount += key.Value;
        _depositCounts[key] =
            _depositCounts.TryGetValue(key, out int value)
            ? ++value : 1;
    }
}
