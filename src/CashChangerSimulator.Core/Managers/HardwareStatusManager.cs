using CashChangerSimulator.Core.Models;
using PosSharp.Abstractions;
using R3;

namespace CashChangerSimulator.Core.Managers;

/// <summary>釣銭機のハードウェア的な障害状態(ジャムなど)を管理するクラス。</summary>
public class HardwareStatusManager : IHardwareStatus, IDisposable
{
    private readonly CompositeDisposable disposables = [];

    // --- 内部入力用 ReactiveProperties ---
    private ReactiveProperty<bool> isClaimedByAnotherInput = null!;
    private ReactiveProperty<bool> deviceEnabledInput = null!;
    private ReactiveProperty<bool> isJammedInput = null!;
    private ReactiveProperty<JamLocation> jamLocationInput = null!;
    private ReactiveProperty<bool> isOverlappedInput = null!;
    private ReactiveProperty<bool> isDeviceErrorInput = null!;
    private ReactiveProperty<bool> isConnectedInput = null!;
    private ReactiveProperty<bool> isCollectionBoxRemovedInput = null!;
    private ReactiveProperty<int?> currentErrorCodeInput = null!;
    private ReactiveProperty<int> currentErrorCodeExtendedInput = null!;
    private ReactiveProperty<bool> isBillRemainingNormalInput = null!;
    private ReactiveProperty<bool> isCoinRemainingNormalInput = null!;
    private ReactiveProperty<bool> isBillRemainingCollectionInput = null!;
    private ReactiveProperty<bool> isCoinRemainingCollectionInput = null!;

    private readonly Dictionary<ExitPort, Dictionary<DenominationKey, int>> exitPortInventories = new()
    {
        { ExitPort.Normal, [] },
        { ExitPort.Collection, [] }
    };

    private Subject<UposStatusUpdateEventArgs> vendorStatusEvents = null!;

    private GlobalLockManager? globalLockManager;
    private bool isDisposed;

    /// <summary><see cref="HardwareStatusManager"/> クラスのインスタンスを生成します。</summary>
    protected HardwareStatusManager()
    {
        InitializeInputProperties();
        SetupPipelines();
        SetupEvents();
        SetupErrorResolutionSideEffects();
    }

    /// <summary>ハードウェアステータスの操作用インターフェースを取得します。</summary>
    public IHardwareStatusInput Input { get; private set; } = null!;

    /// <summary>ハードウェアステータスの監視用インターフェースを取得します。</summary>
    public IHardwareStatus State => this;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsClaimedByAnother { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> DeviceEnabled { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsJammed { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<JamLocation> CurrentJamLocation { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsOverlapped { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsDeviceError { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsConnected { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsCollectionBoxRemoved { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<int?> CurrentErrorCode { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<int> CurrentErrorCodeExtended { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsNormal { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsBillRemainingNormal { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsCoinRemainingNormal { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsBillRemainingCollection { get; private set; } = null!;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsCoinRemainingCollection { get; private set; } = null!;

    /// <inheritdoc/>
    public IReadOnlyDictionary<DenominationKey, int> GetExitPortCounts(ExitPort port)
    {
        lock (exitPortInventories)
        {
            return new Dictionary<DenominationKey, int>(exitPortInventories[port]);
        }
    }

    /// <inheritdoc/>
    public Observable<PosSharp.Abstractions.UposStatusUpdateEventArgs> StatusUpdateEvents { get; private set; } = null!;

    /// <summary>このインスタンスが破棄されているかどうかを取得します。</summary>
    public bool IsDisposed => isDisposed;

    /// <summary>インスタンスを生成します。</summary>
    /// <returns>生成されたインスタンス。</returns>
    public static HardwareStatusManager Create() => new();

    /// <summary>グローバルロックマネージャーを設定します。</summary>
    /// <param name="manager">設定するマネージャー。</param>
    public void SetGlobalLockManager(GlobalLockManager manager) => globalLockManager = manager;

    /// <summary>他者による占有状態をグローバルロックマネージャーから最新化した上で取得します。</summary>
    /// <returns>占有されている場合は true。</returns>
    public bool RefreshClaimedStatus()
    {
        if (isDisposed)
        {
            return false;
        }

        if (globalLockManager == null)
        {
            return isClaimedByAnotherInput.Value;
        }

        var heldByAnother = globalLockManager.IsLockHeldByAnother();
        isClaimedByAnotherInput.Value = heldByAnother;
        return heldByAnother;
    }

    /// <summary>グローバルロックの取得を試みます。</summary>
    /// <returns>取得に成功した場合は true。</returns>
    public bool TryAcquireGlobalLock()
    {
        var result = globalLockManager?.TryAcquire() ?? true;
        if (result)
        {
            isClaimedByAnotherInput.Value = false;
        }

        return result;
    }

    /// <summary>グローバルロックを解放します。</summary>
    public void ReleaseGlobalLock() => globalLockManager?.Release();

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>このインスタンスで使用されているリソースを解放します。</summary>
    /// <param name="disposing">マネージド・リソースを解放する場合は true。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            disposables.Dispose();
            globalLockManager?.Dispose();
        }

        isDisposed = true;
    }

    private void InitializeInputProperties()
    {
        isClaimedByAnotherInput = new ReactiveProperty<bool>(false).AddTo(disposables);
        deviceEnabledInput = new ReactiveProperty<bool>(false).AddTo(disposables);
        isJammedInput = new ReactiveProperty<bool>(false).AddTo(disposables);
        jamLocationInput = new ReactiveProperty<JamLocation>(JamLocation.None).AddTo(disposables);
        isOverlappedInput = new ReactiveProperty<bool>(false).AddTo(disposables);
        isDeviceErrorInput = new ReactiveProperty<bool>(false).AddTo(disposables);
        isConnectedInput = new ReactiveProperty<bool>(false).AddTo(disposables);
        isCollectionBoxRemovedInput = new ReactiveProperty<bool>(false).AddTo(disposables);
        currentErrorCodeInput = new ReactiveProperty<int?>(null).AddTo(disposables);
        currentErrorCodeExtendedInput = new ReactiveProperty<int>(0).AddTo(disposables);

        isBillRemainingNormalInput = new ReactiveProperty<bool>(false).AddTo(disposables);
        isCoinRemainingNormalInput = new ReactiveProperty<bool>(false).AddTo(disposables);
        isBillRemainingCollectionInput = new ReactiveProperty<bool>(false).AddTo(disposables);
        isCoinRemainingCollectionInput = new ReactiveProperty<bool>(false).AddTo(disposables);

        var vendorStatusEventsSubject = new Subject<UposStatusUpdateEventArgs>().AddTo(disposables);
        this.vendorStatusEvents = vendorStatusEventsSubject;

        var resetTrigger = new Subject<Unit>().AddTo(disposables);
        Input = new HardwareStatusInputHandler(this, resetTrigger);

        resetTrigger
            .Subscribe(_ =>
            {
                isJammedInput.Value = false;
                jamLocationInput.Value = JamLocation.None;
                isOverlappedInput.Value = false;
                isDeviceErrorInput.Value = false;
                isCollectionBoxRemovedInput.Value = false;
                currentErrorCodeInput.Value = null;
                currentErrorCodeExtendedInput.Value = 0;
                Input.ClearExitPort(ExitPort.Normal);
                Input.ClearExitPort(ExitPort.Collection);
            })
            .AddTo(disposables);
    }

    private void SetupPipelines()
    {
        IsClaimedByAnother = isClaimedByAnotherInput.ToReadOnlyReactiveProperty().AddTo(disposables);
        DeviceEnabled = deviceEnabledInput.ToReadOnlyReactiveProperty().AddTo(disposables);
        IsJammed = isJammedInput.ToReadOnlyReactiveProperty().AddTo(disposables);

        // JamLocation は IsJammed が false なら強制的に None にするパイプライン
        CurrentJamLocation = isJammedInput
            .CombineLatest(jamLocationInput, (jammed, loc) => jammed ? loc : JamLocation.None)
            .ToReadOnlyReactiveProperty(JamLocation.None)
            .AddTo(disposables);

        IsOverlapped = isOverlappedInput.ToReadOnlyReactiveProperty().AddTo(disposables);
        IsDeviceError = isDeviceErrorInput.ToReadOnlyReactiveProperty().AddTo(disposables);
        IsConnected = isConnectedInput.ToReadOnlyReactiveProperty().AddTo(disposables);
        IsCollectionBoxRemoved = isCollectionBoxRemovedInput.ToReadOnlyReactiveProperty().AddTo(disposables);
        CurrentErrorCode = currentErrorCodeInput.ToReadOnlyReactiveProperty().AddTo(disposables);
        CurrentErrorCodeExtended = currentErrorCodeExtendedInput.ToReadOnlyReactiveProperty().AddTo(disposables);

        IsBillRemainingNormal = isBillRemainingNormalInput.ToReadOnlyReactiveProperty().AddTo(disposables);
        IsCoinRemainingNormal = isCoinRemainingNormalInput.ToReadOnlyReactiveProperty().AddTo(disposables);
        IsBillRemainingCollection = isBillRemainingCollectionInput.ToReadOnlyReactiveProperty().AddTo(disposables);
        IsCoinRemainingCollection = isCoinRemainingCollectionInput.ToReadOnlyReactiveProperty().AddTo(disposables);

        // 合成ステータスの構築: エラーがない場合に true
        IsNormal = Observable.CombineLatest(
            IsJammed,
            IsOverlapped,
            IsDeviceError,
            IsCollectionBoxRemoved,
            (j, o, e, c) => !(j || o || e || c))
            .ToReadOnlyReactiveProperty()
            .AddTo(disposables);
    }

    private void SetupEvents()
    {
        StatusUpdateEvents = Observable.Merge(
            IsConnected.Select(c => new PosSharp.Abstractions.UposStatusUpdateEventArgs((int)(c ? DeviceStatus.PowerOn : DeviceStatus.PowerOff))),
            IsJammed.Select(j => new PosSharp.Abstractions.UposStatusUpdateEventArgs((int)(j ? DeviceStatus.JournalEmpty : DeviceStatus.JournalOk))),
            SetupExitPortEvents(),
            vendorStatusEvents);
    }

    private Observable<UposStatusUpdateEventArgs> SetupExitPortEvents()
    {
        return Observable.Merge(
            IsBillRemainingNormal.DistinctUntilChanged().Select(r => new UposStatusUpdateEventArgs(r ? ExitPortStatusEvents.StatusBillRemainingNormal : ExitPortStatusEvents.StatusBillClearedNormal)),
            IsCoinRemainingNormal.DistinctUntilChanged().Select(r => new UposStatusUpdateEventArgs(r ? ExitPortStatusEvents.StatusCoinRemainingNormal : ExitPortStatusEvents.StatusCoinClearedNormal)),
            IsBillRemainingCollection.DistinctUntilChanged().Select(r => new UposStatusUpdateEventArgs(r ? ExitPortStatusEvents.StatusBillRemainingCollection : ExitPortStatusEvents.StatusBillClearedCollection)),
            IsCoinRemainingCollection.DistinctUntilChanged().Select(r => new UposStatusUpdateEventArgs(r ? ExitPortStatusEvents.StatusCoinRemainingCollection : ExitPortStatusEvents.StatusCoinClearedCollection))
        );
    }

    private void SetupErrorResolutionSideEffects()
    {
        // エラー解除時の副作用 (GlobalLock の解放)
        IsNormal
            .DistinctUntilChanged()
            .Where(isSafe => isSafe)
            .Subscribe(_ =>
            {
                globalLockManager?.Release();
            })
            .AddTo(disposables);
    }

    /// <summary>操作用インターフェースの実装。アクセス修飾子を明示するために内部クラスとして定義します。</summary>
    private sealed class HardwareStatusInputHandler(HardwareStatusManager owner, Subject<Unit> resetTrigger) : IHardwareStatusInput
    {
        public ReactiveProperty<bool> IsClaimedByAnother => owner.isClaimedByAnotherInput;

        public ReactiveProperty<bool> DeviceEnabled => owner.deviceEnabledInput;

        public ReactiveProperty<bool> IsJammed => owner.isJammedInput;

        public ReactiveProperty<JamLocation> CurrentJamLocation => owner.jamLocationInput;

        public ReactiveProperty<bool> IsOverlapped => owner.isOverlappedInput;

        public ReactiveProperty<bool> IsDeviceError => owner.isDeviceErrorInput;

        public ReactiveProperty<bool> IsConnected => owner.isConnectedInput;

        public ReactiveProperty<bool> IsCollectionBoxRemoved => owner.isCollectionBoxRemovedInput;

        public ReactiveProperty<int?> CurrentErrorCode => owner.currentErrorCodeInput;

        public ReactiveProperty<int> CurrentErrorCodeExtended => owner.currentErrorCodeExtendedInput;

        public ReactiveProperty<bool> IsBillRemainingNormal => owner.isBillRemainingNormalInput;
        public ReactiveProperty<bool> IsCoinRemainingNormal => owner.isCoinRemainingNormalInput;
        public ReactiveProperty<bool> IsBillRemainingCollection => owner.isBillRemainingCollectionInput;
        public ReactiveProperty<bool> IsCoinRemainingCollection => owner.isCoinRemainingCollectionInput;

        public void AddExitPortCounts(ExitPort port, IReadOnlyDictionary<DenominationKey, int> counts)
        {
            if (owner.isDisposed) return;

            lock (owner.exitPortInventories)
            {
                var inventory = owner.exitPortInventories[port];
                foreach (var kv in counts)
                {
                    inventory[kv.Key] = inventory.GetValueOrDefault(kv.Key) + kv.Value;
                }

                // 状態更新
                if (counts.Any(c => c.Key.Type == CurrencyCashType.Bill))
                {
                    if (port == ExitPort.Normal) owner.isBillRemainingNormalInput.Value = true;
                    else owner.isBillRemainingCollectionInput.Value = true;
                }
                if (counts.Any(c => c.Key.Type == CurrencyCashType.Coin))
                {
                    if (port == ExitPort.Normal) owner.isCoinRemainingNormalInput.Value = true;
                    else owner.isCoinRemainingCollectionInput.Value = true;
                }
            }
        }

        public void ClearExitPort(ExitPort port)
        {
            if (owner.isDisposed) return;

            lock (owner.exitPortInventories)
            {
                owner.exitPortInventories[port].Clear();

                if (port == ExitPort.Normal)
                {
                    owner.isBillRemainingNormalInput.Value = false;
                    owner.isCoinRemainingNormalInput.Value = false;
                }
                else
                {
                    owner.isBillRemainingCollectionInput.Value = false;
                    owner.isCoinRemainingCollectionInput.Value = false;
                }
            }
        }

        public Subject<Unit> ResetTrigger => resetTrigger;
    }
}
