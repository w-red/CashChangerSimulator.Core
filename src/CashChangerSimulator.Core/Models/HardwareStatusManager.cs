using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>
/// 釣銭機のハードウェア的な障害状態（ジャムなど）を管理するクラス。
/// </summary>
public class HardwareStatusManager : IDisposable
{
    private readonly BindableReactiveProperty<bool> _isJammed = new(false);

    /// <summary>ジャムが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsJammed => _isJammed;

    /// <summary>
    /// ジャム状態を切り替える。
    /// </summary>
    public void SetJammed(bool jammed)
    {
        _isJammed.Value = jammed;
    }

    public void Dispose()
    {
        _isJammed.Dispose();
    }
}
