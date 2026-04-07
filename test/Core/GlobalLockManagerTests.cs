using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Core.Managers;

/// <summary>
/// GlobalLockManager の排他制御機能を検証するテストクラス。
/// Tests for verify GlobalLockManager's synchronization features.
/// </summary>
public class GlobalLockManagerTests : IDisposable
{
    private readonly string _testLockPath;
    private readonly GlobalLockManager _lockManager;

    public GlobalLockManagerTests()
    {
        _testLockPath = Path.Combine(Path.GetTempPath(), $"lock_test_{Guid.NewGuid()}.lock");
        _lockManager = new GlobalLockManager(_testLockPath, NullLogger.Instance);
    }

    /// <summary>
    /// 各テストの終了時にリソースを解放します。
    /// Release resources after each test.
    /// </summary>
    public void Dispose()
    {
        _lockManager.Dispose();
        if (File.Exists(_testLockPath))
        {
            try { File.Delete(_testLockPath); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// ロックの取得、保持確認、解放が正しく動作することを検証します。
    /// Verify that lock acquisition, hold check, and release work correctly.
    /// </summary>
    [Fact]
    public void TestAcquireAndRelease()
    {
        // 1. 最初は保持されていない
        _lockManager.IsLockHeldByAnother().ShouldBeFalse();

        // 2. 取得成功
        _lockManager.TryAcquire().ShouldBeTrue();

        // 3. 自分で保持している場合、IsLockHeldByAnother は false (他者ではない)
        _lockManager.IsLockHeldByAnother().ShouldBeFalse();

        // 4. 再帰的な呼び出し（既に保持している場合は true）
        _lockManager.TryAcquire().ShouldBeTrue();

        // 5. 解放
        _lockManager.Release();
        _lockManager.IsLockHeldByAnother().ShouldBeFalse();
    }

    /// <summary>
    /// Dispose 後のロック操作がエラーを発生させないことを検証します。
    /// Verify that locking operations after Dispose do not crash.
    /// </summary>
    [Fact]
    public void TestLockAfterDispose()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"lock_dispose_{Guid.NewGuid()}.lock");
        var tempManager = new GlobalLockManager(path, NullLogger.Instance);
        tempManager.Dispose();

        try
        {
            // Act & Assert
            Should.NotThrow(() =>
            {
                tempManager.TryAcquire();
                tempManager.Release();
            });
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// 別スレッドがロックを保持している間、他者がロックを取得できないことを検証します。
    /// Verify that others cannot acquire the lock while another thread holds it.
    /// </summary>
    [Fact]
    public async Task TestConcurrentLocking()
    {
        // Arrange
        var lockAcquired = new TaskCompletionSource<bool>();
        var lockReleased = new TaskCompletionSource<bool>();
        
        using var otherManager = new GlobalLockManager(_testLockPath, NullLogger.Instance);

        // スレッド1でロックを取得
        var t1 = Task.Run(() =>
        {
            if (_lockManager.TryAcquire())
            {
                lockAcquired.SetResult(true);
                // スレッド2が試行するまで待機
                lockReleased.Task.Wait(5000);
                _lockManager.Release();
                return true;
            }
            return false;
        });

        await lockAcquired.Task;

        // Act - スレッド2で別のインスタンスから同じパスでロックを試行
        var t2 = Task.Run(() =>
        {
            // 他者が持っているので失敗するはず
            return otherManager.TryAcquire();
        });

        var result2 = await t2;
        result2.ShouldBeFalse();
        
        // 他者が保持していることを確認
        otherManager.IsLockHeldByAnother().ShouldBeTrue();

        // スレッド1を解放
        lockReleased.SetResult(true);
        await t1;

        // スレッド1が解放されたので、次は取得できるはず
        otherManager.TryAcquire().ShouldBeTrue();
        otherManager.IsLockHeldByAnother().ShouldBeFalse();
    }

    /// <summary>
    /// コンストラクタに空のパスを渡した場合、デフォルトのパスが使用されることを検証します。
    /// Verify that default path is used when an empty path is passed to the constructor.
    /// </summary>
    [Fact]
    public void ConstructorShouldUseDefaultPathIfNullOrEmpty()
    {
        // Act
        using var manager = new GlobalLockManager("", NullLogger.Instance);
        
        // Assert
        // プロパティへの直接アクセスはできないが、動作が正常であることを確認
        manager.IsLockHeldByAnother().ShouldBeFalse();
        manager.TryAcquire().ShouldBeTrue();
        manager.Release();
    }

    /// <summary>
    /// 二重に Dispose を呼び出しても例外が発生しないことを検証します。
    /// Verify that calling Dispose twice does not throw exceptions.
    /// </summary>
    [Fact]
    public void DoubleDisposeShouldBeSafe()
    {
        // Arrange
        var manager = new GlobalLockManager(Path.Combine(Path.GetTempPath(), $"double_dispose_{Guid.NewGuid()}.lock"), NullLogger.Instance);
        
        // Act & Assert
        Should.NotThrow(() =>
        {
            manager.Dispose();
            manager.Dispose();
        });
    }

    /// <summary>
    /// 外部のファイルストリームによってロックされている場合、IsLockHeldByAnother が true を返すことを検証します。
    /// Verify that IsLockHeldByAnother returns true when the file is locked by an external stream.
    /// </summary>
    [Fact]
    public void IsLockHeldByAnotherShouldBeTrueWhenFileLockedByExternalStream()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"external_lock_{Guid.NewGuid()}.lock");
        try
        {
            // 1. 他のストリームで排他ロック
            using var fs = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            
            using var manager = new GlobalLockManager(path, NullLogger.Instance);
            
            // Act & Assert
            // 2. _fileStream が null かつ外部が保持しているので、true になるはず
            manager.IsLockHeldByAnother().ShouldBeTrue();
        }
        finally
        {
            if (File.Exists(path)) try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    /// <summary>アクセス権限がない場合に TryAcquire が false を返すことを検証します。</summary>
    [Fact]
    public void TryAcquireShouldReturnFalseWhenAccessDenied()
    {
        // Arrange
        // ディレクトリパスをファイルパスとして渡すと、FileStream コンストラクタで UnauthorizedAccessException が発生する
        var tempDir = Path.Combine(Path.GetTempPath(), $"dir_lock_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            using var manager = new GlobalLockManager(tempDir, NullLogger.Instance);
            
            // Act
            var result = manager.TryAcquire();
            
            // Assert
            result.ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
        }
    }

    /// <summary>アクセス権限がない場合に IsLockHeldByAnother が false を返すことを検証します。</summary>
    [Fact]
    public void IsLockHeldByAnotherShouldReturnFalseWhenAccessDenied()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"dir_check_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            using var manager = new GlobalLockManager(tempDir, NullLogger.Instance);
            
            // Act
            var result = manager.IsLockHeldByAnother();
            
            // Assert
            // 実装上、UnauthorizedAccessException は不保持（false）として扱う
            result.ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
        }
    }

    /// <summary>ロックファイルのディレクトリが存在しない場合に自動で作成されることを検証します。</summary>
    [Fact]
    public void ConstructorShouldCreateDirectoryIfMissing()
    {
        // Arrange
        var tempRoot = Path.Combine(Path.GetTempPath(), $"root_{Guid.NewGuid()}");
        var lockFile = Path.Combine(tempRoot, "subdir", "test.lock");
        
        try
        {
            // Act
            using var manager = new GlobalLockManager(lockFile, NullLogger.Instance);
            
            // Assert
            Directory.Exists(Path.GetDirectoryName(lockFile)).ShouldBeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }
}
