using System.Diagnostics.CodeAnalysis;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using R3;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>釣銭機ハードウェアの動作をシミュレートするクラス。</summary>
public class HardwareSimulator : IDeviceSimulator
{
    [SuppressMessage("Microsoft.Reliability", "CA2213:DisposableFieldsShouldBeDisposed", Justification = "Field is managed via CompositeDisposable in constructors.")]
    private readonly ConfigurationProvider? configProvider;
    private readonly TimeProvider timeProvider;
    private readonly CompositeDisposable disposables = [];

    /// <summary>Initializes a new instance of the <see cref="HardwareSimulator"/> class.デフォルト設定でシミュレーターを初期化する。</summary>
    private HardwareSimulator()
        : this(null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HardwareSimulator"/> class.設定プロバイダーを指定してシミュレーターを初期化する。</summary>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="timeProvider">時間プロバイダー。</param>
    [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "AddTo(disposables) ensures proper disposal.")]
    [SuppressMessage("Microsoft.Reliability", "CA2213:DisposableFieldsShouldBeDisposed", Justification = "Field is disposed via disposables collection.")]
    private HardwareSimulator(ConfigurationProvider? configProvider, TimeProvider? timeProvider = null)
    {
        if (configProvider == null)
        {
            var internalConfig = new ConfigurationProvider();
            disposables.Add(internalConfig);
            this.configProvider = internalConfig;
        }
        else
        {
            this.configProvider = configProvider;
        }

        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>デフォルト設定でシミュレーターを生成・初期化します。</summary>
    /// <param name="timeProvider">時間プロバイダー。</param>
    /// <returns>初期化済みの <see cref="HardwareSimulator"/> インスタンス。</returns>
    public static HardwareSimulator Create(TimeProvider? timeProvider = null)
    {
        return new HardwareSimulator(null, timeProvider);
    }

    /// <summary>設定プロバイダーを指定してシミュレーターを生成・初期化します。</summary>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="timeProvider">時間プロバイダー。</param>
    /// <returns>初期化済みの <see cref="HardwareSimulator"/> インスタンス。</returns>
    public static HardwareSimulator Create(ConfigurationProvider? configProvider, TimeProvider? timeProvider = null)
    {
        return new HardwareSimulator(configProvider, timeProvider);
    }

    /// <inheritdoc/>
    public async Task SimulateDispenseAsync(CancellationToken ct = default)
    {
        var delay = configProvider?.Config?.Simulation?.DispenseDelayMs ?? 500;
        if (delay > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(delay), timeProvider, ct).ConfigureAwait(false);
        }
    }

    /// <summary>リソースを破棄します。</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>マネージリソースを破棄します。</summary>
    /// <param name="disposing">マネージリソースを破棄する場合は true。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposables.Dispose();
        }
    }
}
