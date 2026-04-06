using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Core.Managers;

/// <summary>OS レベルのファイルロックを利用して、プロセス間でのデバイス占有（Claim）を制御するマネージャー。.</summary>
/// <remarks>
/// .NET の FileShare.None を利用して、ファイルを「開きっぱなし」にすることでロックを維持します。
/// プロセスがクラッシュした場合は OS が自動的にハンドルを閉じるため、ゴーストロックが発生しません。.
/// </remarks>
public sealed class GlobalLockManager : IDisposable
{
    private readonly string lockFilePath;
    private readonly ILogger logger;
    private FileStream? lockStream;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="GlobalLockManager"/> class.指定されたファイルパスを使用してロックマネージャーを初期化します。.</summary>
    /// <param name="lockFilePath">ロックに使用するファイルパス。.</param>
    /// <param name="logger">ロガー。.</param>
    public GlobalLockManager(string lockFilePath, ILogger logger)
    {
        this.lockFilePath = lockFilePath;
        this.logger = logger;

        // Ensure directory exists
        var dir = Path.GetDirectoryName(this.lockFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    /// <summary>現在、他者（別プロセスまたは別ハンドル）によってロックが保持されているかどうかを確認します。.</summary>
    /// <returns>既に他者が保持している場合は true。.</returns>
    public bool IsLockHeldByAnother()
    {
        if (lockStream != null)
        {
            return false; // 自分が保持している
        }

        // プロセス終了直後や破棄直後にファイルハンドルがOSによって完全に解放されるまで、
        // わずかな時間がかかる場合があるため、リトライループを設ける。
        for (int i = 0; i < 3; i++)
        {
            try
            {
                // Try to open with shared access.
                // If someone else has it with FileShare.None, this will fail with IOException (Sharing Violation).
                using var stream = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                return false;
            }
            catch (IOException)
            {
                // Sharing violation usually means another process has the file open with FileShare.None
                if (i == 2)
                {
                    return true;
                }

                System.Threading.Thread.Sleep(20);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.ZLogWarning($"Access denied checking global lock: {ex.Message}");
                return false; // テストの期待値および「アクセス不能＝自分が保持していない」状態に合わせる
            }
        }

        return false;
    }

    /// <summary>排他的なロックを取得しようと試みます。.</summary>
    /// <returns>取得に成功した場合は true、既に他者が保持している場合は false。.</returns>
    public bool TryAcquire()
    {
        if (lockStream != null)
        {
            return true; // 既に自分が保持している
        }

        try
        {
            lockStream = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            logger.ZLogInformation($"Global lock acquired: {lockFilePath}");
            return true;
        }
        catch (IOException)
        {
            // Another process is holding the lock
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.ZLogWarning($"Access denied acquiring global lock: {ex.Message}");
            return false;
        }
    }

    /// <summary>現在保持しているロックを解放します。.</summary>
    public void Release()
    {
        if (lockStream != null)
        {
            lockStream.Dispose();
            lockStream = null;
            logger.ZLogInformation($"Global lock released: {lockFilePath}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Release();
        disposed = true;
    }
}
