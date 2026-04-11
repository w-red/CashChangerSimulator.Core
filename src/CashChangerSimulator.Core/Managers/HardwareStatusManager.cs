using System;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using R3;

namespace CashChangerSimulator.Core.Managers;

/// <summary>釣銭機のハードウェア的な障害状態(ジャムなど)を管理するクラス。</summary>
public class HardwareStatusManager : IHardwareStatus, IDisposable
{
    private readonly CompositeDisposable disposables = [];

    // --- 内部入力用 ReactiveProperties ---
    private readonly ReactiveProperty<bool> isClaimedByAnotherInput;
    private readonly ReactiveProperty<bool> deviceEnabledInput;
    private readonly ReactiveProperty<bool> isJammedInput;
    private readonly ReactiveProperty<JamLocation> jamLocationInput;
    private readonly ReactiveProperty<bool> isOverlappedInput;
    private readonly ReactiveProperty<bool> isDeviceErrorInput;
    private readonly ReactiveProperty<bool> isConnectedInput;
    private readonly ReactiveProperty<bool> isCollectionBoxRemovedInput;
    private readonly ReactiveProperty<int?> currentErrorCodeInput;
    private readonly ReactiveProperty<int> currentErrorCodeExtendedInput;

    private GlobalLockManager? globalLockManager;
    private bool isDisposed;

    /// <summary><see cref="HardwareStatusManager"/> クラスのインスタンスを生成します。</summary>
    protected HardwareStatusManager()
    {
        // 1. 各入力口の初期化
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
        var resetTrigger = new Subject<Unit>().AddTo(disposables);
        Input = new HardwareStatusInputHandler(this, resetTrigger);

        // 2. リセットトリガーの購読 (入力値のリセット)
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
            })
            .AddTo(disposables);

        // 3. 宣言的な読み取り専用プロパティ (State) の構築
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

        // 4. 合成イベントの構築
        StatusUpdateEvents = Observable.Merge(
            IsConnected.Select(c => new DeviceStatusUpdateEventArgs((int)(c ? DeviceStatus.PowerOn : DeviceStatus.PowerOff))),
            IsJammed.Select(j => new DeviceStatusUpdateEventArgs((int)(j ? DeviceStatus.JournalEmpty : DeviceStatus.JournalOk))));

        // 5. 合成ステータスの構築
        // エラーがない(Jammed, Overlapped, DeviceError, BoxRemoved がすべて false)場合に true
        IsNormal = Observable.CombineLatest(
            IsJammed,
            IsOverlapped,
            IsDeviceError,
            IsCollectionBoxRemoved,
            (j, o, e, c) => !(j || o || e || c))
            .ToReadOnlyReactiveProperty()
            .AddTo(disposables);

        // 6. エラー解除時の副作用 (GlobalLock の解放)
        // 全てのエラーフラグが false になった瞬間を捉える
        IsNormal
            .DistinctUntilChanged()
            .Where(isSafe => isSafe)
            .Subscribe(_ =>
            {
                // エラー解除時の副作用としてロックを解放 (冪等性は Release 側で保証)
                globalLockManager?.Release();
            })
            .AddTo(disposables);
    }

    /// <summary>ハードウェアステータスの操作用インターフェースを取得します。</summary>
    public IHardwareStatusInput Input { get; }

    /// <summary>ハードウェアステータスの監視用インターフェースを取得します。</summary>
    public IHardwareStatus State => this;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsClaimedByAnother { get; }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> DeviceEnabled { get; }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsJammed { get; }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<JamLocation> CurrentJamLocation { get; }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsOverlapped { get; }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsDeviceError { get; }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsConnected { get; }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsCollectionBoxRemoved { get; }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<int?> CurrentErrorCode { get; }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<int> CurrentErrorCodeExtended { get; }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsNormal { get; }

    /// <inheritdoc/>
    public Observable<DeviceStatusUpdateEventArgs> StatusUpdateEvents { get; }

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
        if (IsDisposed)
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

            // globalLockManager の破棄は、オーナーが別である可能性が高いためここでは行わないか、検討が必要。
            // 以前のコードでは破棄していたので、踏襲する。
            globalLockManager?.Dispose();
        }

        isDisposed = true;
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

        public Subject<Unit> ResetTrigger => resetTrigger;
    }
}
