using CashChangerSimulator.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using ZLogger;

namespace CashChangerSimulator.Core;

/// <summary>アプリケーション全体で共有される ILoggerFactory を管理するクラス。</summary>
public static class LogProvider
{
    private static ILoggerFactory? _factory;

    /// <summary>全体で共有するロガーファクトリ。初期化前は NullLoggerFactory を返します。</summary>
    public static ILoggerFactory Factory => _factory ?? NullLoggerFactory.Instance;

    /// <summary>ロギング設定に基づいて LogProvider を初期化します。</summary>
    /// <param name="settings">ロギング設定。</param>
    public static void Initialize(LoggingSettings settings)
    {
        _factory = LoggerFactory.Create(builder =>
        {
            // ログレベルの設定
            if (Enum.TryParse<LogLevel>(settings.LogLevel, out var level))
            {
                builder.SetMinimumLevel(level);
            }
            else
            {
                builder.SetMinimumLevel(LogLevel.Information);
            }

            // コンソール出力の設定
            if (settings.EnableConsole)
            {
                builder.AddZLoggerConsole(options =>
                {
                    options.UsePlainTextFormatter(formatter =>
                    {
                        formatter.SetPrefixFormatter($"{0:local-longdate} [{1:short}] ",
                            (in template, in info) => template.Format(info.Timestamp, info.LogLevel));
                    });
                });
            }

            // ファイル出力の設定
            if (settings.EnableFile)
            {
                var directory = settings.LogDirectory;
                var fileName = settings.LogFileName;
                var path = Path.Combine(directory, fileName);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                builder.AddZLoggerFile(path, options =>
                {
                    options.UsePlainTextFormatter(formatter =>
                    {
                        formatter.SetPrefixFormatter($"{0:local-longdate} [{1:short}] ",
                            (in template, in info) => template.Format(info.Timestamp, info.LogLevel));
                    });
                });
            }
        });
    }

    /// <summary>指定された型のロガーを生成します。</summary>
    /// <typeparam name="T">ロガーを使用するクラスの型。</typeparam>
    /// <returns>ILogger インスタンス。</returns>
    public static ILogger<T> CreateLogger<T>() => Factory.CreateLogger<T>();
}
