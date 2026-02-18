using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>
/// 釣銭機のハードウェア的な障害状態（ジャムなど）を管理するクラス。
/// </summary>
public class HardwareStatusManager : IDisposable
{
    private readonly BindableReactiveProperty<bool> _isJammed = new(false);
    private readonly BindableReactiveProperty<bool> _isOverlapped = new(false);

    /// <summary>ジャムが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsJammed => _isJammed;

    /// <summary>紙幣などの重なり（バリデーションエラー）が発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsOverlapped => _isOverlapped;

    /// <summary>
    /// ジャム状態を切り替える。
    /// </summary>
    public void SetJammed(bool jammed)
    {
        _isJammed.Value = jammed;
    }

    /// <summary>
    /// 重なり状態を切り替える。
    /// </summary>
    public void SetOverlapped(bool overlapped)
    {
        _isOverlapped.Value = overlapped;
    }

    public void Dispose()
    {
        _isJammed.Dispose();
        _isOverlapped.Dispose();
    }
}
