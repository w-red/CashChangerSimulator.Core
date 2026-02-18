using Microsoft.PointOfService;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Configuration;
using R3;

namespace CashChangerSimulator.Device;

/// <summary>
/// UPOS v1.5+ の Deposit シーケンスを管理するコントローラー。
/// beginDeposit → fixDeposit → endDeposit の状態遷移とバリデーションを担う。
/// </summary>
public class DepositController : IDisposable
{
    private readonly Inventory _inventory;
    private readonly CashChangerManager _manager;
    private readonly SimulationSettings _config;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly CompositeDisposable _disposables = [];
    private readonly Subject<Unit> _changed = new();

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed => _changed;
    
    private decimal _depositAmount;
    private readonly Dictionary<DenominationKey, int> _depositCounts = [];
    private CashDepositStatus _depositStatus = CashDepositStatus.None;
    private bool _depositPaused;
    private bool _depositFixed;

    public DepositController(
        Inventory inventory, 
        CashChangerManager manager, 
        SimulationSettings? config = null,
        HardwareStatusManager? hardwareStatusManager = null)
    {
        _inventory = inventory;
        _manager = manager;
        _config = config ?? new SimulationSettings();
        _hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();
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

    /// <summary>入金が確定（固定）されたかどうか。</summary>
    public bool IsFixed => _depositFixed;

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
        _hardwareStatusManager.SetOverlapped(false); // Clear error on new deposit
        _depositStatus = CashDepositStatus.Count;
        _changed.OnNext(Unit.Default);
    }

    /// <summary>
    /// UPOS 8.5.6: 入金を確定する。
    /// beginDeposit が先に呼ばれていない場合は E_ILLEGAL。
    /// </summary>
    public void FixDeposit()
    {
        if (!IsDepositInProgress) throw new PosControlException("Deposit not in progress", ErrorCode.Illegal);

        _depositFixed = true;
        _changed.OnNext(Unit.Default);
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

        if (action == CashDepositAction.Repay)
        {
            foreach (var kv in _depositCounts)
            {
                _inventory.Add(kv.Key, -kv.Value);
            }
        }
        else
        {
            // For NoChange/Change, we check if device is healthy
            if (_hardwareStatusManager.IsOverlapped.Value)
            {
                throw new PosControlException("Device is in error state (Overlap). Cannot complete deposit.", ErrorCode.Failure);
            }

            if (action == CashDepositAction.Change)
            {
                if (_depositAmount > 0)
                {
                    _manager.Dispense(_depositAmount);
                }
            }
        }
        _depositStatus = CashDepositStatus.End;
        _depositPaused = false;
        _depositFixed = false;
        _hardwareStatusManager.SetOverlapped(false); // Clear error on end deposit
        _changed.OnNext(Unit.Default);
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
        _changed.OnNext(Unit.Default);
    }

    /// <summary>
    /// 入金中に金種が追加されたときに呼ばれるトラッキングメソッド。
    /// </summary>
    public void TrackDeposit(DenominationKey key) 
        => TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, 1 } });

    /// <summary>
    /// 入金中に複数の金種を一括で追加するトラッキングメソッド。
    /// </summary>
    public void TrackBulkDeposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        if (_depositStatus != CashDepositStatus.Count) return;
        if (_depositPaused) return;
        if (_hardwareStatusManager.IsOverlapped.Value) return;

        foreach (var (key, count) in counts)
        {
            if (count <= 0) continue;

            // Simulation: Validation Failure (Overlap etc.)
            if (_config.RandomErrorsEnabled)
            {
                var roll = Random.Shared.Next(0, 100);
                if (roll < _config.ValidationFailureRate)
                {
                    _hardwareStatusManager.SetOverlapped(true);
                    _changed.OnNext(Unit.Default);
                    return; // Stop processing further items
                }
            }

            _depositAmount += key.Value * count;
            _depositCounts[key] =
                _depositCounts.TryGetValue(key, out int value)
                ? value + count : count;
            
            // Fix: Update physical inventory immediately upon insertion
            // Note: We bypass the circular dependency in SimulatorCashChanger 
            // by ensuring TrackBulkDeposit is the one driving the change.
            _inventory.Add(key, count);
        }
        _changed.OnNext(Unit.Default);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _changed.OnCompleted();
    }
}
