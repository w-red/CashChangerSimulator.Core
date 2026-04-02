using R3;

namespace CashChangerSimulator.Core.Managers;

/// <summary>釣銭機のハードウェア的な障害状態（ジャムなど）を管理するクラス。</summary>
/// <remarks>
/// デバイスの物理的な状態（接続、ジャム、エラーコード）を保持します。
/// 各状態は `BindableReactiveProperty` で提供され、UI やロジック層からの監視が可能です。
/// </remarks>
public class HardwareStatusManager : IDisposable
{
    private GlobalLockManager? _globalLockManager;

    public BindableReactiveProperty<bool> IsClaimedByAnother { get; } = new(false);

    /// <summary>ジャムが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsJammed { get; } = new(false);

    /// <summary>ジャムが発生している具体的な箇所。</summary>
    public BindableReactiveProperty<Models.JamLocation> JamLocation { get; } = new(Models.JamLocation.None);

    /// <summary>紙幣などの重なり（バリデーションエラー）が発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsOverlapped { get; } = new(false);

    /// <summary>個別の特定可能なエラー（ジャムなど）以外の、一般的なデバイスエラーが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsDeviceError { get; } = new(false);

    /// <summary>デバイスが論理的に接続（Open）されているかどうか。</summary>
    public BindableReactiveProperty<bool> IsConnected { get; } = new(false);

    /// <summary>回収庫が取り外されているかどうか。</summary>
    public BindableReactiveProperty<bool> IsCollectionBoxRemoved { get; } = new(false);

    /// <summary>現在発生中のデバイスエラーの ErrorCode 値 (Nullable)。</summary>
    public BindableReactiveProperty<int?> CurrentErrorCode { get; } = new(null);

    /// <summary>現在発生中のデバイスエラーの ErrorCodeExtended 値。</summary>
    public BindableReactiveProperty<int> CurrentErrorCodeExtended { get; } = new(0);

    /// <summary>このインスタンスが破棄されているかどうかを取得します。</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>ジャム状態を切り替えます。箇所を指定することも可能です。</summary>
    public void SetJammed(bool jammed, Models.JamLocation location = Models.JamLocation.None)
    {
        if (IsDisposed) return;
        IsJammed.Value = jammed;
        JamLocation.Value = jammed ? location : Models.JamLocation.None;
    }

    /// <summary>重なり状態を切り替えます。</summary>
    public void SetOverlapped(bool overlapped)
    {
        if (IsDisposed) return;
        IsOverlapped.Value = overlapped;
    }

    /// <summary>接続状態を切り替えます。</summary>
    public void SetConnected(bool connected)
    {
        if (IsDisposed) return;
        IsConnected.Value = connected;
    }

    /// <summary>回収庫の取り外し状態を設定します。</summary>
    public void SetCollectionBoxRemoved(bool removed)
    {
        if (IsDisposed) return;
        IsCollectionBoxRemoved.Value = removed;
    }

    /// <summary>他者による占有（Claim）状態を設定します。</summary>
    public void SetClaimedByAnother(bool claimed)
    {
        if (IsDisposed) return;
        IsClaimedByAnother.Value = claimed;
    }

    /// <summary>グローバルロックマネージャーを設定します。</summary>
    public void SetGlobalLockManager(GlobalLockManager manager)
    {
        if (IsDisposed) return;
        _globalLockManager = manager;
    }

    /// <summary>
    /// 他者による占有状態をグローバルロックマネージャーから最新化した上で取得します。
    /// </summary>
    public bool RefreshClaimedStatus()
    {
        if (IsDisposed) return false;
        if (_globalLockManager == null) return IsClaimedByAnother.Value;

        var heldByAnother = _globalLockManager.IsLockHeldByAnother();
        IsClaimedByAnother.Value = heldByAnother;
        return heldByAnother;
    }

    /// <summary>グローバルロックの取得を試みます。</summary>
    public bool TryAcquireGlobalLock()
    {
        var result = _globalLockManager?.TryAcquire() ?? true;
        if (result)
        {
            IsClaimedByAnother.Value = false;
        }
        else
        {
            IsClaimedByAnother.Value = true;
        }
        return result;
    }

    /// <summary>グローバルロックを解放します。</summary>
    public void ReleaseGlobalLock() => _globalLockManager?.Release();

    /// <summary>デバイスエラー状態とそのエラーコードを設定します。</summary>
    /// <param name="errorCode">発生した ErrorCode の整数値</param>
    /// <param name="errorCodeExtended">追加の詳細エラーコード</param>
    public void SetDeviceError(int errorCode, int errorCodeExtended = 0)
    {
        if (IsDisposed) return;
        CurrentErrorCode.Value = errorCode;
        CurrentErrorCodeExtended.Value = errorCodeExtended;
        IsDeviceError.Value = true;
    }

    /// <summary>すべてのエラー状態を解除します。</summary>
    public void ResetError()
    {
        if (IsDisposed) return;
        IsJammed.Value = false;
        JamLocation.Value = Models.JamLocation.None;
        IsOverlapped.Value = false;
        IsDeviceError.Value = false;
        IsCollectionBoxRemoved.Value = false;
        CurrentErrorCode.Value = null;
        CurrentErrorCodeExtended.Value = 0;
        _globalLockManager?.Release();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        if (disposing)
        {
            IsJammed.Dispose();
            JamLocation.Dispose();
            IsOverlapped.Dispose();
            IsDeviceError.Dispose();
            IsConnected.Dispose();
            IsCollectionBoxRemoved.Dispose();
            IsClaimedByAnother.Dispose();
            CurrentErrorCode.Dispose();
            CurrentErrorCodeExtended.Dispose();
            _globalLockManager?.Dispose();
        }
        IsDisposed = true;
    }
}
