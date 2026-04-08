using System.Diagnostics.CodeAnalysis;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using R3;

namespace CashChangerSimulator.Core.Managers;

/// <summary>釣銭機のハードウェア的な障害状態（ジャムなど）を管理するクラス。</summary>
/// <remarks>
/// デバイスの物理的な状態（接続、ジャム、エラーコード）を保持します。
/// 各状態は `BindableReactiveProperty` で提供され、UI やロジック層からの監視が可能です。
/// </remarks>
public class HardwareStatusManager : IDisposable
{
    private readonly CompositeDisposable disposables = [];
    private GlobalLockManager? globalLockManager;

    /// <summary>Initializes a new instance of the <see cref="HardwareStatusManager"/> class.</summary>
    [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "AddTo(disposables) ensures proper disposal.")]
    protected HardwareStatusManager()
    {
        IsClaimedByAnother = new BindableReactiveProperty<bool>(false).AddTo(disposables);
        DeviceEnabled = new BindableReactiveProperty<bool>(false).AddTo(disposables);
        IsJammed = new BindableReactiveProperty<bool>(false).AddTo(disposables);
        JamLocation = new BindableReactiveProperty<Models.JamLocation>(Models.JamLocation.None).AddTo(disposables);
        IsOverlapped = new BindableReactiveProperty<bool>(false).AddTo(disposables);
        IsDeviceError = new BindableReactiveProperty<bool>(false).AddTo(disposables);
        IsConnected = new BindableReactiveProperty<bool>(false).AddTo(disposables);
        IsCollectionBoxRemoved = new BindableReactiveProperty<bool>(false).AddTo(disposables);
        CurrentErrorCode = new BindableReactiveProperty<int?>(null).AddTo(disposables);
        CurrentErrorCodeExtended = new BindableReactiveProperty<int>(0).AddTo(disposables);
    }

    /// <summary>他のプロセスやインスタンスによってデバイスが占有されているかどうかを保持するプロパティ。</summary>
    public BindableReactiveProperty<bool> IsClaimedByAnother { get; }

    /// <summary>デバイスが有効化されているかどうか。</summary>
    public BindableReactiveProperty<bool> DeviceEnabled { get; }

    /// <summary>ジャムが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsJammed { get; }

    /// <summary>ジャムが発生している具体的な箇所。</summary>
    public BindableReactiveProperty<Models.JamLocation> JamLocation { get; }

    /// <summary>紙幣などの重なり（バリデーションエラー）が発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsOverlapped { get; }

    /// <summary>個別の特定可能なエラー（ジャムなど）以外の、一般的なデバイスエラーが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsDeviceError { get; }

    /// <summary>デバイスが論理的に接続（Open）されているかどうか。</summary>
    public BindableReactiveProperty<bool> IsConnected { get; }

    /// <summary>回収庫が取り外されているかどうか。</summary>
    public BindableReactiveProperty<bool> IsCollectionBoxRemoved { get; }

    /// <summary>現在発生中のデバイスエラーの ErrorCode 値 (Nullable)。</summary>
    public BindableReactiveProperty<int?> CurrentErrorCode { get; }

    /// <summary>現在発生中のデバイスエラーの ErrorCodeExtended 値。</summary>
    public BindableReactiveProperty<int> CurrentErrorCodeExtended { get; }

    /// <summary>ステータス情報の更新通知イベント。</summary>
    public Observable<DeviceStatusUpdateEventArgs> StatusUpdateEvents =>
        Observable.Merge(
            IsConnected.Select(c => new DeviceStatusUpdateEventArgs((int)(c ? DeviceStatus.PowerOn : DeviceStatus.PowerOff))),
            IsJammed.Select(j => new DeviceStatusUpdateEventArgs((int)(j ? DeviceStatus.JournalEmpty : DeviceStatus.JournalOk)))); // 簡易的なマッピング

    /// <summary>このインスタンスが破棄されているかどうかを取得します。</summary>
    public bool IsDisposed => disposables.IsDisposed;

    /// <summary>ハードウェアステータスマネージャーを生成・初期化します。</summary>
    /// <returns>初期化済みの <see cref="HardwareStatusManager"/> インスタンス。</returns>
    public static HardwareStatusManager Create()
    {
        return new HardwareStatusManager();
    }

    /// <summary>ジャム状態を切り替えます。箇所を指定することも可能です。</summary>
    /// <param name="jammed">ジャムが発生している場合は true。</param>
    /// <param name="location">ジャムの発生箇所。</param>
    public void SetJammed(bool jammed, Models.JamLocation location = Models.JamLocation.None)
    {
        if (IsDisposed)
        {
            return;
        }

        IsJammed.Value = jammed;
        JamLocation.Value = jammed ? location : Models.JamLocation.None;
    }

    /// <summary>重なり状態を切り替えます。</summary>
    /// <param name="overlapped">重なりが発生している場合は true。</param>
    public void SetOverlapped(bool overlapped)
    {
        if (IsDisposed)
        {
            return;
        }

        IsOverlapped.Value = overlapped;
    }

    /// <summary>接続状態を切り替えます。</summary>
    /// <param name="connected">接続されている場合は true。</param>
    public void SetConnected(bool connected)
    {
        if (IsDisposed)
        {
            return;
        }

        IsConnected.Value = connected;
    }

    /// <summary>デバイスの有効状態を切り替えます。</summary>
    /// <param name="enabled">有効な場合は true。</param>
    public void SetDeviceEnabled(bool enabled)
    {
        if (IsDisposed)
        {
            return;
        }

        DeviceEnabled.Value = enabled;
    }

    /// <summary>回収庫の取り外し状態を設定します。</summary>
    /// <param name="removed">取り外されている場合は true。</param>
    public void SetCollectionBoxRemoved(bool removed)
    {
        if (IsDisposed)
        {
            return;
        }

        IsCollectionBoxRemoved.Value = removed;
    }

    /// <summary>他者による占有（Claim）状態を設定します。</summary>
    /// <param name="claimed">占有されている場合は true。</param>
    public void SetClaimedByAnother(bool claimed)
    {
        if (IsDisposed)
        {
            return;
        }

        IsClaimedByAnother.Value = claimed;
    }

    /// <summary>グローバルロックマネージャーを設定します。</summary>
    /// <param name="manager">設定するマネージャー。</param>
    public void SetGlobalLockManager(GlobalLockManager manager)
    {
        if (IsDisposed)
        {
            return;
        }

        globalLockManager = manager;
    }

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
            return IsClaimedByAnother.Value;
        }

        var heldByAnother = globalLockManager.IsLockHeldByAnother();
        IsClaimedByAnother.Value = heldByAnother;
        return heldByAnother;
    }

    /// <summary>グローバルロックの取得を試みます。</summary>
    /// <returns>取得の成否</returns>
    public bool TryAcquireGlobalLock()
    {
        var result = globalLockManager?.TryAcquire() ?? true;
        IsClaimedByAnother.Value = !result;
        return result;
    }

    /// <summary>グローバルロックを解放します。</summary>
    public void ReleaseGlobalLock() =>
        globalLockManager?.Release();

    /// <summary>デバイスエラー状態とそのエラーコードを設定します。</summary>
    /// <param name="errorCode">発生した ErrorCode の整数値.</param>
    /// <param name="errorCodeExtended">追加の詳細エラーコード.</param>
    public void SetDeviceError(
        int errorCode,
        int errorCodeExtended = 0)
    {
        if (IsDisposed)
        {
            return;
        }

        CurrentErrorCode.Value = errorCode;
        CurrentErrorCodeExtended.Value = errorCodeExtended;
        IsDeviceError.Value = true;
    }

    /// <summary>すべてのエラー状態を解除します。</summary>
    public void ResetError()
    {
        if (IsDisposed)
        {
            return;
        }

        IsJammed.Value = false;
        JamLocation.Value = Models.JamLocation.None;
        IsOverlapped.Value = false;
        IsDeviceError.Value = false;
        IsCollectionBoxRemoved.Value = false;
        CurrentErrorCode.Value = null;
        CurrentErrorCodeExtended.Value = 0;
        globalLockManager?.Release();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを破棄します。</summary>
    /// <param name="disposing">マネージリソースを破棄する場合は true。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposables.Dispose();
            globalLockManager?.Dispose();
        }
    }
}
