using R3;

namespace CashChangerSimulator.Core.Managers;

/// <summary>釣銭機のハードウェア的な障害状態（ジャムなど）を管理するクラス。</summary>
public class HardwareStatusManager : IDisposable
{
    private readonly BindableReactiveProperty<bool> _isJammed = new(false);
    private readonly BindableReactiveProperty<bool> _isOverlapped = new(false);
    private readonly BindableReactiveProperty<bool> _isDeviceError = new(false);
    private readonly BindableReactiveProperty<bool> _isConnected = new(true); // デフォルトは接続済み(HOT)
    private readonly BindableReactiveProperty<int?> _currentErrorCode = new(null);
    private readonly BindableReactiveProperty<int> _currentErrorCodeExtended = new(0);

    /// <summary>ジャムが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsJammed => _isJammed;

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

    /// <summary>ジャム状態を切り替えます。</summary>
    public void SetJammed(bool jammed)
    {
        _isJammed.Value = jammed;
    }

    /// <summary>重なり状態を切り替えます。</summary>
    public void SetOverlapped(bool overlapped)
    {
        _isOverlapped.Value = overlapped;
    }

    /// <summary>接続状態を切り替えます。</summary>
    public void SetConnected(bool connected)
    {
        _isConnected.Value = connected;
    }

    /// <summary>デバイスエラー状態とそのエラーコードを設定します。</summary>
    /// <param name="errorCode">発生した ErrorCode の整数値</param>
    /// <param name="errorCodeExtended">追加の詳細エラーコード</param>
    public void SetDeviceError(int errorCode, int errorCodeExtended = 0)
    {
        _currentErrorCode.Value = errorCode;
        _currentErrorCodeExtended.Value = errorCodeExtended;
        _isDeviceError.Value = true;
    }

    /// <summary>すべてのエラー状態を解除します。</summary>
    public void ResetError()
    {
        _isJammed.Value = false;
        _isOverlapped.Value = false;
        _isDeviceError.Value = false;
        _currentErrorCode.Value = null;
        _currentErrorCodeExtended.Value = 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _isJammed.Dispose();
        _isOverlapped.Dispose();
        _isDeviceError.Dispose();
        _currentErrorCode.Dispose();
        _currentErrorCodeExtended.Dispose();
        GC.SuppressFinalize(this);
    }
}
