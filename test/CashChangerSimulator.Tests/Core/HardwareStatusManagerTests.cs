using CashChangerSimulator.Core.Managers;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>HardwareStatusManager の状態管理とライフサイクルを検証するテスト。</summary>
public class HardwareStatusManagerTests
{
    /// <summary>ジャム状態が正しく設定・保持されることを検証する。</summary>
    [Fact]
    public void SetJammedShouldUpdateSubject()
    {
        // Arrange
        using var manager = new HardwareStatusManager();
        bool statusChanged = false;
        using var _ = manager.IsJammed.Subscribe(val => statusChanged = val);

        // Act
        manager.SetJammed(true);

        // Assert
        manager.IsJammed.Value.ShouldBeTrue();
        statusChanged.ShouldBeTrue();

        // Act: Reset
        manager.SetJammed(false);
        manager.IsJammed.Value.ShouldBeFalse();
    }

    /// <summary>重なり状態が正しく設定・保持されることを検証する。</summary>
    [Fact]
    public void SetOverlappedShouldUpdateSubject()
    {
        // Arrange
        using var manager = new HardwareStatusManager();
        bool statusChanged = false;
        using var _ = manager.IsOverlapped.Subscribe(val => statusChanged = val);

        // Act
        manager.SetOverlapped(true);

        // Assert
        manager.IsOverlapped.Value.ShouldBeTrue();
        statusChanged.ShouldBeTrue();
    }

    /// <summary>ジャムと重なりが独立して動作することを検証する。</summary>
    [Fact]
    public void JamAndOverlapShouldBeIndependent()
    {
        // Arrange
        using var manager = new HardwareStatusManager();

        // Act
        manager.SetJammed(true);
        manager.SetOverlapped(true);

        // Assert
        manager.IsJammed.Value.ShouldBeTrue();
        manager.IsOverlapped.Value.ShouldBeTrue();

        // Act: Clear Jam
        manager.SetJammed(false);
        manager.IsJammed.Value.ShouldBeFalse();
        manager.IsOverlapped.Value.ShouldBeTrue();
    }

    /// <summary>Dispose 呼び出しによりリソースが解放されることを検証する（カバレッジ用）。</summary>
    [Fact]
    public void DisposeShouldWork()
    {
        // Arrange
        var manager = new HardwareStatusManager();

        // Act
        manager.Dispose();

        // Assert: Dispose 済みを直接確認する公開フラグはないが、例外が起きないことを確認
        Should.NotThrow(() => manager.Dispose());
    }
}
