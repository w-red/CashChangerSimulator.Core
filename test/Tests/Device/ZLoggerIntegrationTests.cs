using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using Microsoft.Extensions.Logging;
using Shouldly;
using ZLogger;

namespace CashChangerSimulator.Tests.Device;

/// <summary>ZLogger による非同期例外の出力およびスタックトレースの保持を検証するテストクラス。</summary>
public class ZLoggerIntegrationTests : IDisposable
{
    private readonly string _testLogDir;
    private readonly string _testLogFile = "zlogger_verify.log";

    public ZLoggerIntegrationTests()
    {
        _testLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_logs");
        if (Directory.Exists(_testLogDir)) Directory.Delete(_testLogDir, true);
        Directory.CreateDirectory(_testLogDir);
    }

    [Fact]
    public async Task ZLoggerShouldRecordAsyncExceptionWithStackTrace()
    {
        // Arrange
        var settings = new LoggingSettings
        {
            EnableConsole = false,
            EnableFile = true,
            LogDirectory = _testLogDir,
            LogFileName = _testLogFile,
            LogLevel = "Information"
        };
        LogProvider.Initialize(settings);
        var logger = LogProvider.CreateLogger<ZLoggerIntegrationTests>();
        var exceptionMessage = "Test Async Exception Message";

        // Act
        await Task.Run(() =>
        {
            try
            {
                ThrowNestedException();
            }
            catch (Exception ex)
            {
                // ここで例外オブジェクトを渡すことが重要
                logger.ZLogError(ex, $"Caught async error: {ex.Message}");
            }
        });

        // ZLogger はバックグラウンドスレッドで書き込むため、Dispose してフラッシュを強制する
        LogProvider.Dispose();

        // Assert
        var logPath = Path.Combine(_testLogDir, _testLogFile);
        File.Exists(logPath).ShouldBeTrue();

        var logContent = File.ReadAllText(logPath);
        
        // メッセージが含まれているか
        logContent.ShouldContain("Caught async error");
        logContent.ShouldContain(exceptionMessage);
        
        // スタックトレース（メソッド名など）が含まれているか
        logContent.ShouldContain("ThrowNestedException");
        logContent.ShouldContain("ZLoggerIntegrationTests.cs");
        
        // ZLogger v2 の構造化ログにより Exception 型名が含まれているか
        logContent.ShouldContain("System.InvalidOperationException");
    }

    private void ThrowNestedException()
    {
        throw new InvalidOperationException("Test Async Exception Message");
    }

    public void Dispose()
    {
        LogProvider.Dispose(); // テスト終了時に確実に解放
        if (Directory.Exists(_testLogDir))
        {
            try { Directory.Delete(_testLogDir, true); } catch { /* Ignore */ }
        }
    }
}
