using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using ZLogger;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.Device;

/// <summary>入金シーケンスのライフサイクルを管理するコントローラー。</summary>
/// <remarks>
/// 入金プロセスの開始（<see cref="BeginDeposit"/>）、計数（<see cref="TrackBulkDeposit"/>）、一時停止（<see cref="PauseDeposit"/>）、
/// および在庫への反映（<see cref="EndDeposit"/>/<see cref="FixDeposit"/>）の一連の流れを制御します。
/// </remarks>
/// <param name="inventory">入金額を最終的に反映させる <see cref="Inventory"/> モデル。</param>
/// <param name="hardwareStatusManager">デバイスの状態を管理する <see cref="HardwareStatusManager"/>。未指定時は新規作成されます。</param>
/// <param name="manager">在庫操作を統括する <see cref="CashChangerManager"/>。</param>
/// <param name="configProvider">金種設定などを提供する <see cref="ConfigurationProvider"/>。未指定時は新規作成されます。</param>
public class DepositController(
    Inventory inventory,
    HardwareStatusManager? hardwareStatusManager = null,
    CashChangerManager? manager = null,
    ConfigurationProvider? configProvider = null) : IDisposable
{
    private static T EnsureNotNull<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }

    private readonly Inventory _inventory = EnsureNotNull(inventory);
    private readonly HardwareStatusManager _hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();
    private readonly ConfigurationProvider _configProvider = configProvider ?? new ConfigurationProvider();
    
    /// <summary>リアルタイムデータの通知能力を外部から受け取ります。</summary>
    public bool RealTimeDataEnabled { get; set; }

    private readonly ILogger<DepositController> _logger = LogProvider.CreateLogger<DepositController>();
    private readonly CompositeDisposable _disposables = [];
    private readonly Subject<Unit> _changed = new();

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public virtual Observable<Unit> Changed => _changed;

    private decimal _depositAmount;
    private decimal _overflowAmount;
    private decimal _rejectAmount;
    private readonly Dictionary<DenominationKey, int> _depositCounts = [];
    private readonly List<string> _depositedSerials = [];
    private readonly List<string> _lastDepositedSerials = [];
    private CashDepositStatus _depositStatus = CashDepositStatus.None;
    private bool _depositPaused;
    private bool _depositFixed;

    // ========== Properties ==========

    /// <summary>入金合計額。</summary>
    public virtual decimal DepositAmount => _depositAmount;

    /// <summary>今回の入金でオーバーフロー（回収庫行き）となった金額。</summary>
    public virtual decimal OverflowAmount => _overflowAmount;

    /// <summary>今回の入金でリジェクト（返却）された金額。</summary>
    public virtual decimal RejectAmount => _rejectAmount;

    /// <summary>金種ごとの入金枚数。</summary>
    public virtual IReadOnlyDictionary<DenominationKey, int> DepositCounts => _depositCounts;

    /// <summary>入金ステータス。</summary>
    public virtual CashDepositStatus DepositStatus => _depositStatus;

    /// <summary>入金受付中かどうかを取得します。</summary>
    /// <remarks>払出ガードなどの判定に使用されます。</remarks>
    public virtual bool IsDepositInProgress =>
        _depositStatus is CashDepositStatus.Start or CashDepositStatus.Count;

    /// <summary>一時停止中かどうか。</summary>
    public virtual bool IsPaused => _depositPaused;

    /// <summary>入金が確定（固定）されたかどうか。</summary>
    public virtual bool IsFixed => _depositFixed;

    /// <summary>直前の入金セッションで投入されたシリアル番号一覧。</summary>
    public virtual IReadOnlyList<string> LastDepositedSerials => _lastDepositedSerials;

    // ========== Methods ==========

    /// <summary>入金受付を開始します。</summary>
    public virtual void BeginDeposit()
    {
        _logger.ZLogInformation($"BeginDeposit called. Current Status: {_depositStatus}");

        _depositAmount = 0m;
        _overflowAmount = 0m;
        _rejectAmount = 0m;
        _depositCounts.Clear();
        _depositedSerials.Clear();
        _depositStatus = CashDepositStatus.Start;
        _depositPaused = false;
        _depositFixed = false;

        if (_hardwareStatusManager.IsJammed.Value)
        {
            throw new PosControlException("Device is jammed. Cannot begin deposit.", ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.Jam);
        }
        if (_hardwareStatusManager.IsOverlapped.Value)
        {
            throw new PosControlException("Device has overlapped cash. Cannot begin deposit.", ErrorCode.Failure);
        }
        _depositStatus = CashDepositStatus.Count;
        _changed.OnNext(Unit.Default);
        _logger.ZLogInformation($"BeginDeposit finished. New Status: {_depositStatus}");
    }

    /// <summary>投入された現金の計数を確定（固定）します。</summary>
    public virtual void FixDeposit()
    {
        if (!IsDepositInProgress) throw new PosControlException("Deposit not in progress", ErrorCode.Illegal);

        _depositFixed = true;
        _lastDepositedSerials.Clear();
        _lastDepositedSerials.AddRange(_depositedSerials);
        _changed.OnNext(Unit.Default);
    }

    /// <summary>入金処理を終了します。</summary>
    public virtual void EndDeposit(CashDepositAction action)
    {
        if (!_depositFixed)
        {
            throw new PosControlException(
                "The call sequence is invalid. fixDeposit must be called before endDeposit.",
                ErrorCode.Illegal);
        }

        if (action == CashDepositAction.Repay)
        {
            // (Since we haven't added to inventory yet, we don't need to subtract)
            _logger.ZLogInformation($"Deposit Repay: Returning cash without updating inventory.");
        }
        else
        {
            // Store (Change/NoChange): Commit deposit to inventory via Manager
            if (manager != null)
            {
                manager.Deposit(new Dictionary<DenominationKey, int>(_depositCounts));
            }
            else
            {
                // Fallback for tests lacking manager injection
                foreach (var kv in _depositCounts)
                {
                    _inventory.Add(kv.Key, kv.Value);
                }
            }
        }
        // For NoChange/Change, we check if device is healthy
        // Repay is allowed even if overlapped to recover money.
        if (action != CashDepositAction.Repay && _hardwareStatusManager.IsOverlapped.Value)
        {
            throw new PosControlException("Device is in error state (Overlap). Cannot complete deposit.", ErrorCode.Failure);
        }

        _depositStatus = CashDepositStatus.End;
        _depositPaused = false;
        _depositFixed = false;
        _depositAmount = 0m;
        _overflowAmount = 0m;
        _rejectAmount = 0m;
        _depositCounts.Clear();
        _changed.OnNext(Unit.Default);
    }

    /// <summary>投入された現金を返却し、入金セッションを終了します。</summary>
    public virtual void RepayDeposit()
    {
        if (_depositStatus is not (CashDepositStatus.Start or CashDepositStatus.Count))
        {
            throw new PosControlException("Deposit session is not active.", ErrorCode.Illegal);
        }

        _logger.ZLogInformation($"RepayDeposit: Returning accepted cash ({_depositAmount}).");
        _depositStatus = CashDepositStatus.End;
        _depositPaused = false;
        _depositFixed = false;
        _depositAmount = 0m;
        _overflowAmount = 0m;
        _rejectAmount = 0m;
        _depositCounts.Clear();
        _depositedSerials.Clear();
        _changed.OnNext(Unit.Default);
    }

    /// <summary>入金受付を停止します。</summary>
    public virtual void PauseDeposit(CashDepositPause control)
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

    /// <summary>入金中に金種が追加されたときに呼ばれるトラッキングメソッド。</summary>
    public void TrackDeposit(DenominationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, 1 } });
    }

    /// <summary>入金中に複数の金種を一括で追加します。</summary>
    public void TrackBulkDeposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (_depositStatus != CashDepositStatus.Count) return;
        if (_depositPaused) return;

        if (_hardwareStatusManager.IsJammed.Value)
        {
            throw new PosControlException("Device is jammed. Cannot track deposit.", ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.Jam);
        }
        if (_hardwareStatusManager.IsOverlapped.Value)
        {
            throw new PosControlException("Device has overlapped cash. Cannot track deposit.", ErrorCode.Failure);
        }

        foreach (var (key, count) in counts)
        {
            if (count <= 0) continue;

            // オーバーフロー・リサイクル可否のチェック
            var setting = _configProvider.Config.GetDenominationSetting(key);
            var currentInInventory = inventory.GetCount(key);
            var currentInDeposit = _depositCounts.GetValueOrDefault(key, 0);
            
            if (setting.IsRecyclable)
            {
                var totalAfter = currentInInventory + currentInDeposit + count;
                if (totalAfter > setting.Full)
                {
                    var overflowCount = totalAfter - Math.Max(currentInInventory + currentInDeposit, setting.Full);
                    if(overflowCount > count) overflowCount = count;

                    if (overflowCount > 0)
                    {
                        _overflowAmount += overflowCount * key.Value;
                        _logger.ZLogInformation($"Overflow detected for {key}. Count: {overflowCount}. Routing to collection box. Inventory: {currentInInventory}, InDeposit: {currentInDeposit}, Adding: {count}, Full: {setting.Full}");
                    }
                }
            }

            _depositAmount += key.Value * count;
            _depositCounts[key] =
                _depositCounts.TryGetValue(key, out int value)
                ? value + count : count;

            // Logic Correction: Inventory is NOT updated here.
            // It will be updated in EndDeposit if action is Store.

            // Record serial numbers for Bills
            if (key.Type == CurrencyCashType.Bill)
            {
                for (int i = 0; i < count; i++)
                {
                    var serial = $"S{key.Value}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
                    _depositedSerials.Add(serial);
                }
            }
        }
        _changed.OnNext(Unit.Default);
    }

    /// <summary>シミュレーションとして入金リジェクト（返却）を発生させます。</summary>
    public void SimulateReject(decimal amount)
    {
        if (_depositStatus != CashDepositStatus.Count) return;
        _rejectAmount += amount;
        _logger.ZLogInformation($"Simulated Reject of {amount}. Total Reject: {_rejectAmount}");
        _changed.OnNext(Unit.Default);
    }
    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        _changed.OnCompleted();
        GC.SuppressFinalize(this);
    }
}
