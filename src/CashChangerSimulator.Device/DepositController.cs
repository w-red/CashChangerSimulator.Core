using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using MicroResolver;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using ZLogger;
using CashChangerSimulator.Core.Opos;

namespace CashChangerSimulator.Device;

/// <summary>入金シーケンスのライフサイクルを管理するコントローラー。</summary>
public class DepositController : IDisposable
{
    private readonly Inventory _inventory;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly CashChangerManager? _manager;

    /// <summary>在庫を指定して初期化する。</summary>
    public DepositController(Inventory inventory) : this(inventory, null, null) { }

    /// <summary>在庫とステータスマネージャーを指定して初期化する。</summary>
    [Inject]
    public DepositController(Inventory inventory, HardwareStatusManager? hardwareStatusManager = null, CashChangerManager? manager = null)
    {
        _inventory = inventory;
        _hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();
        _manager = manager;
    }

    private readonly ILogger<DepositController> _logger = LogProvider.CreateLogger<DepositController>();
    private readonly CompositeDisposable _disposables = [];
    private readonly Subject<Unit> _changed = new();

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public virtual Observable<Unit> Changed => _changed;

    private decimal _depositAmount;
    private readonly Dictionary<DenominationKey, int> _depositCounts = [];
    private readonly List<string> _depositedSerials = [];
    private readonly List<string> _lastDepositedSerials = [];
    private CashDepositStatus _depositStatus = CashDepositStatus.None;
    private bool _depositPaused;
    private bool _depositFixed;

    // ========== Properties ==========

    /// <summary>入金合計額。</summary>
    public virtual decimal DepositAmount => _depositAmount;

    /// <summary>金種ごとの入金枚数。</summary>
    public virtual IReadOnlyDictionary<DenominationKey, int> DepositCounts => _depositCounts;

    /// <summary>入金ステータス。</summary>
    public virtual CashDepositStatus DepositStatus => _depositStatus;

    /// <summary>入金受付中かどうか（払出ガード判定用）。</summary>
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
    public void BeginDeposit()
    {
        _logger.ZLogInformation($"BeginDeposit called. Current Status: {_depositStatus}");

        if (!_hardwareStatusManager.IsConnected.Value)
        {
            throw new PosControlException("Device is not open (Closed).", ErrorCode.Closed);
        }

        _depositAmount = 0m;
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

    /// <summary>投入された金額を確定（固定）します。</summary>
    public void FixDeposit()
    {
        if (!IsDepositInProgress) throw new PosControlException("Deposit not in progress", ErrorCode.Illegal);

        _depositFixed = true;
        _lastDepositedSerials.Clear();
        _lastDepositedSerials.AddRange(_depositedSerials);
        _changed.OnNext(Unit.Default);
    }

    /// <summary>入金受付を完了し、必要に応じて在庫を更新します。</summary>
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
            // (Since we haven't added to inventory yet, we don't need to subtract)
            _logger.ZLogInformation($"Deposit Repay: Returning cash without updating inventory.");
        }
        else
        {
            // Store (Change/NoChange): Commit deposit to inventory via Manager
            if (_manager != null)
            {
                _manager.Deposit(new Dictionary<DenominationKey, int>(_depositCounts));
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
        _depositCounts.Clear();
        _changed.OnNext(Unit.Default);
    }

    /// <summary>入金受付の一時停止または再開を制御します。</summary>
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

    /// <summary>入金中に金種が追加されたときに呼ばれるトラッキングメソッド。</summary>
    public void TrackDeposit(DenominationKey key)
        => TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, 1 } });

    /// <summary>入金中に複数の金種を一括で追加します。</summary>
    public void TrackBulkDeposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        if (_depositStatus != CashDepositStatus.Count) return;
        if (_depositPaused) return;

        if (!_hardwareStatusManager.IsConnected.Value)
        {
            throw new PosControlException("Device is not open (Closed).", ErrorCode.Closed);
        }

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

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        _changed.OnCompleted();
        GC.SuppressFinalize(this);
    }
}
