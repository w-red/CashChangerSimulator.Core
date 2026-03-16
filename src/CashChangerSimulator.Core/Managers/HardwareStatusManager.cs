using R3;

namespace CashChangerSimulator.Core.Managers;

/// <summary>釣銭機のハードウェア的な障害状態（ジャムなど）を管理するクラス。</summary>
/// <remarks>
/// デバイスの物理的な状態（接続、ジャム、エラーコード）を保持します。
/// 各状態は `BindableReactiveProperty` で提供され、UI やロジック層からの監視が可能です。
/// </remarks>
public class HardwareStatusManager : IDisposable
{
    private readonly BindableReactiveProperty<bool> _isJammed = new(false);
    private readonly BindableReactiveProperty<Models.JamLocation> _jamLocation = new(Models.JamLocation.None);
    private readonly BindableReactiveProperty<bool> _isOverlapped = new(false);
    private readonly BindableReactiveProperty<bool> _isDeviceError = new(false);
    private readonly BindableReactiveProperty<bool> _isConnected = new(false); // Default is disconnected (COLD start baseline)
    private readonly BindableReactiveProperty<int?> _currentErrorCode = new(null);
    private readonly BindableReactiveProperty<int> _currentErrorCodeExtended = new(0);

    /// <summary>ジャムが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsJammed => _isJammed;

    /// <summary>ジャムが発生している具体的な箇所。</summary>
    public BindableReactiveProperty<Models.JamLocation> JamLocation => _jamLocation;

    /// <summary>紙幣などの重なり（バリデーションエラー）が発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsOverlapped => _isOverlapped;

    /// <summary>個別の特定可能なエラー（ジャムなど）以外の、一般的なデバイスエラーが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsDeviceError => _isDeviceError;

    /// <summary>デバイスが論理的に接続（Open）されているかどうか。</summary>
    public BindableReactiveProperty<bool> IsConnected => _isConnected;

    /// <summary>現在発生中のデバイスエラーの ErrorCode 値 (Nullable)。</summary>
    public BindableReactiveProperty<int?> CurrentErrorCode => _currentErrorCode;

    /// <summary>現在発生中のデバイスエラーの ErrorCodeExtended 値。</summary>
    public BindableReactiveProperty<int> CurrentErrorCodeExtended => _currentErrorCodeExtended;

    private bool _disposed;

    /// <summary>このインスタンスが破棄されているかどうかを取得します。</summary>
    public bool IsDisposed => _disposed;

    /// <summary>ジャム状態を切り替えます。箇所を指定することも可能です。</summary>
    public void SetJammed(bool jammed, Models.JamLocation location = Models.JamLocation.None)
    {
        if (_disposed) return;
        _isJammed.Value = jammed;
        _jamLocation.Value = jammed ? location : Models.JamLocation.None;
    }

    /// <summary>重なり状態を切り替えます。</summary>
    public void SetOverlapped(bool overlapped)
    {
        if (_disposed) return;
        _isOverlapped.Value = overlapped;
    }

    /// <summary>接続状態を切り替えます。</summary>
    public void SetConnected(bool connected)
    {
        if (_disposed) return;
        _isConnected.Value = connected;
    }

    /// <summary>デバイスエラー状態とそのエラーコードを設定します。</summary>
    /// <param name="errorCode">発生した ErrorCode の整数値</param>
    /// <param name="errorCodeExtended">追加の詳細エラーコード</param>
    public void SetDeviceError(int errorCode, int errorCodeExtended = 0)
    {
        if (_disposed) return;
        _currentErrorCode.Value = errorCode;
        _currentErrorCodeExtended.Value = errorCodeExtended;
        _isDeviceError.Value = true;
    }

    /// <summary>すべてのエラー状態を解除します。</summary>
    public void ResetError()
    {
        if (_disposed) return;
        _isJammed.Value = false;
        _jamLocation.Value = Models.JamLocation.None;
        _isOverlapped.Value = false;
        _isDeviceError.Value = false;
        _currentErrorCode.Value = null;
        _currentErrorCodeExtended.Value = 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _isJammed.Dispose();
            _jamLocation.Dispose();
            _isOverlapped.Dispose();
            _isDeviceError.Dispose();
            _isConnected.Dispose();
            _currentErrorCode.Dispose();
            _currentErrorCodeExtended.Dispose();
        }
        _disposed = true;
    }
}
