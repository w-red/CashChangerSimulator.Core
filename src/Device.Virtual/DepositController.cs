using CashChangerSimulator.Device;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;
using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>入金シーケンスのライフサイクルを管理するコントローラー（仮想デバイス実装）。</summary>
/// <remarks>
/// UPOS などのプラットフォーム固有の SDK に依存せず、純粋な C# ロジックとして入金プロセスをシミュレートします。
/// </remarks>
public class DepositController : IDisposable
{
    private static T EnsureNotNull<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }

    private readonly Inventory _inventory;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly ConfigurationProvider _configProvider;
    private readonly ConfigurationProvider? _internalConfigProvider;
    private readonly CashChangerManager? _manager;

    public DepositController(
        Inventory inventory,
        HardwareStatusManager? hardwareStatusManager = null,
        CashChangerManager? manager = null,
        ConfigurationProvider? configProvider = null)
    {
        _inventory = EnsureNotNull(inventory);
        _hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();
        if (configProvider == null)
        {
            _configProvider = new ConfigurationProvider();
            _internalConfigProvider = _configProvider;
        }
        else
        {
            _configProvider = configProvider;
            _internalConfigProvider = null;
        }
        _manager = manager;
    }

    /// <summary>リアルタイムデータの通知。上位層（アダプター等）が利用します。</summary>
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
    private DeviceDepositStatus _depositStatus = DeviceDepositStatus.None;
    private bool _depositPaused;
    private bool _depositFixed;

    // ========== Properties ==========

    public virtual decimal DepositAmount => _depositAmount;
    public virtual decimal OverflowAmount => _overflowAmount;
    public virtual decimal RejectAmount => _rejectAmount;
    public virtual IReadOnlyDictionary<DenominationKey, int> DepositCounts => _depositCounts;
    public virtual DeviceDepositStatus DepositStatus => _depositStatus;
    
    public virtual bool IsDepositInProgress =>
        _depositStatus is DeviceDepositStatus.Start or DeviceDepositStatus.Counting;

    public virtual bool IsPaused => _depositPaused;
    public virtual bool IsFixed => _depositFixed;
    public virtual IReadOnlyList<string> LastDepositedSerials => _lastDepositedSerials;

    private decimal _requiredAmount;
    public virtual decimal RequiredAmount
    {
        get => _requiredAmount;
        set
        {
            if (_requiredAmount == value) return;
            _requiredAmount = value;
            _changed.OnNext(Unit.Default);
        }
    }

    // ========== Methods ==========

    public virtual void BeginDeposit()
    {
        _logger.ZLogInformation($"BeginDeposit called. Current Status: {_depositStatus}");

        _depositAmount = 0m;
        _overflowAmount = 0m;
        _rejectAmount = 0m;
        _depositCounts.Clear();
        _depositedSerials.Clear();
        _depositStatus = DeviceDepositStatus.Start;
        _depositPaused = false;
        _depositFixed = false;

        if (_hardwareStatusManager.IsJammed.Value)
        {
            throw new DeviceException("Device is jammed. Cannot begin deposit.", DeviceErrorCode.Jammed);
        }
        if (_hardwareStatusManager.IsOverlapped.Value)
        {
            throw new DeviceException("Device has overlapped cash. Cannot begin deposit.", DeviceErrorCode.Overlapped);
        }
        _depositStatus = DeviceDepositStatus.Counting;
        _inventory.ClearEscrow();
        _changed.OnNext(Unit.Default);
    }

    public virtual void FixDeposit()
    {
        if (_depositStatus != DeviceDepositStatus.Start && _depositStatus != DeviceDepositStatus.Counting)
            throw new DeviceException("Invalid state", DeviceErrorCode.Illegal);

        _depositFixed = true;
        _lastDepositedSerials.Clear();
        _lastDepositedSerials.AddRange(_depositedSerials);
        _changed.OnNext(Unit.Default);
    }

    public virtual void EndDeposit(DepositAction action)
    {
        if (!_depositFixed)
        {
            throw new DeviceException("Invalid call sequence: FixDeposit must be called before EndDeposit.", DeviceErrorCode.Illegal);
        }

        if (action == DepositAction.Repay)
        {
            _logger.ZLogInformation($"Deposit Repay: Returning cash from escrow.");
            _inventory.ClearEscrow();
        }
        else
        {
            // Logic for Store (Updating inventory)
            decimal changeAmount = Math.Max(0, _depositAmount - RequiredAmount);
            var storeCounts = new Dictionary<DenominationKey, int>(_depositCounts);
            var dispenseCounts = new Dictionary<DenominationKey, int>();

            if (changeAmount > 0)
            {
                var availableInEscrow = _inventory.EscrowCounts.OrderByDescending(kv => kv.Key.Value).ToList();
                decimal remainingChange = changeAmount;
                foreach (var (key, countInEscrow) in availableInEscrow)
                {
                    if (remainingChange <= 0) break;
                    int useCount = (int)Math.Min(countInEscrow, Math.Floor(remainingChange / key.Value));
                    if (useCount > 0)
                    {
                        dispenseCounts[key] = useCount;
                        storeCounts[key] -= useCount;
                        remainingChange -= key.Value * useCount;
                    }
                }
                _inventory.ClearEscrow();
                foreach(var kv in storeCounts) if (kv.Value > 0) _inventory.AddEscrow(kv.Key, kv.Value);
                
                if (remainingChange > 0 && _manager != null)
                {
                    _manager.Dispense(remainingChange);
                }
            }
            else
            {
                _inventory.ClearEscrow();
            }

            if (_manager != null)
            {
                _manager.Deposit(new Dictionary<DenominationKey, int>(storeCounts));
            }
            else
            {
                foreach (var kv in storeCounts) if (kv.Value > 0) _inventory.Add(kv.Key, kv.Value);
            }
        }

        if (action != DepositAction.Repay && _hardwareStatusManager.IsOverlapped.Value)
        {
            throw new DeviceException("Device Error (Overlap). Cannot complete deposit.");
        }

        _depositStatus = DeviceDepositStatus.End;
        _depositPaused = false;
        _depositFixed = false;
        _depositAmount = 0m;
        _depositCounts.Clear();
        _inventory.ClearEscrow();
        _changed.OnNext(Unit.Default);
    }

    /// <summary>投入された現金を返却し、入金セッションを終了します。</summary>
    public virtual void RepayDeposit()
    {
        if (!_depositFixed) FixDeposit();
        EndDeposit(DepositAction.Repay);
    }

    public virtual void PauseDeposit(DeviceDepositPause control)
    {
        if (!IsDepositInProgress) throw new DeviceException("Session not active.");
        _depositPaused = (control == DeviceDepositPause.Pause);
        _changed.OnNext(Unit.Default);
    }

    public void TrackDeposit(DenominationKey key, int count = 1)
    {
        TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, count } });
    }

    public void TrackBulkDeposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (_depositStatus != DeviceDepositStatus.Counting || _depositPaused) return;

        if (_hardwareStatusManager.IsJammed.Value)
        {
            throw new DeviceException("Device is jammed during tracking.", DeviceErrorCode.Jammed);
        }
        if (_hardwareStatusManager.IsOverlapped.Value)
        {
            throw new DeviceException("Device has overlapped cash during tracking.", DeviceErrorCode.Overlapped);
        }

        foreach (var (key, count) in counts)
        {
            if (count <= 0) continue;
            var setting = _configProvider.Config.GetDenominationSetting(key);
            
            if (setting.IsRecyclable)
            {
                var currentTotal = _inventory.GetCount(key) + _depositCounts.GetValueOrDefault(key, 0) + count;
                if (currentTotal > setting.Full)
                {
                    _overflowAmount += (currentTotal - setting.Full) * key.Value;
                }
            }

            _depositAmount += key.Value * count;
            _depositCounts[key] = _depositCounts.GetValueOrDefault(key, 0) + count;
            _inventory.AddEscrow(key, count);

            if (key.Type == CurrencyCashType.Bill)
            {
                for (int i = 0; i < count; i++) _depositedSerials.Add($"S{key.Value}-{Guid.NewGuid():N}");
            }
        }
        _changed.OnNext(Unit.Default);
    }

    /// <summary>リジェクトされた現金額をシミュレートします（テスト・シミュレーション用）。</summary>
    public void SimulateReject(decimal amount)
    {
        if (!IsDepositInProgress || _depositPaused) return;
        _rejectAmount += amount;
        _changed.OnNext(Unit.Default);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _changed.OnCompleted();
        _internalConfigProvider?.Dispose();
        GC.SuppressFinalize(this);
    }
}
