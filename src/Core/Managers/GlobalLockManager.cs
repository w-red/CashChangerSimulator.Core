using System.IO;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Core.Managers;

/// <summary>OS レベルのファイルロックを利用して、プロセス間でのデバイス占有（Claim）を制御するマネージャー。</summary>
/// <remarks>
/// .NET の FileShare.None を利用して、ファイルを「開きっぱなし」にすることでロックを維持します。
/// プロセスがクラッシュした場合は OS が自動的にハンドルを閉じるため、ゴーストロックが発生しません。
/// </remarks>
public sealed class GlobalLockManager : IDisposable
{
    private readonly string _lockFilePath;
    private readonly ILogger _logger;
    private FileStream? _lockStream;

    /// <summary>指定されたファイルパスを使用してロックマネージャーを初期化します。</summary>
    public GlobalLockManager(string lockFilePath, ILogger logger)
    {
        _lockFilePath = lockFilePath;
        _logger = logger;
        
        // Ensure directory exists
        var dir = Path.GetDirectoryName(_lockFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    /// <summary>現在、他者（別プロセスまたは別ハンドル）によってロックが保持されているかどうかを確認します。</summary>
    public bool IsLockHeldByAnother()
    {
        if (_lockStream != null) return false; // 自分が保持している

        try
        {
            // Try to open with shared access. 
            // If someone else has it with FileShare.None, this will fail with IOException (Sharing Violation).
            using var stream = new FileStream(_lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            return false;
        }
        catch (IOException)
        {
            // Sharing violation usually means another process has the file open with FileShare.None
            return true;
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning($"Unexpected error checking global lock: {ex.Message}");
            return false;
        }
    }

    /// <summary>排他的なロックを取得しようと試みます。</summary>
    /// <returns>取得に成功した場合は true、既に他者が保持している場合は false。</returns>
    public bool TryAcquire()
    {
        if (_lockStream != null) return true; // 既に自分が保持している

        try
        {
            _lockStream = new FileStream(_lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            _logger.ZLogInformation($"Global lock acquired: {_lockFilePath}");
            return true;
        }
        catch (IOException)
        {
            // Another process is holding the lock
            return false;
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning($"Failed to acquire global lock: {ex.Message}");
            return false;
        }
    }

    /// <summary>現在保持しているロックを解放します。</summary>
    public void Release()
    {
        if (_lockStream != null)
        {
            _lockStream.Dispose();
            _lockStream = null;
            _logger.ZLogInformation($"Global lock released: {_lockFilePath}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Release();
    }
}
