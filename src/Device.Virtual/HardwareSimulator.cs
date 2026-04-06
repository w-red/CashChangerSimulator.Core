using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>釣銭機ハードウェアの動作をシミュレートするクラス。.</summary>
public class HardwareSimulator : IDeviceSimulator
{
    private readonly ConfigurationProvider? configProvider;
    private readonly ConfigurationProvider? internalConfigProvider;

    /// <summary>Initializes a new instance of the <see cref="HardwareSimulator"/> class.デフォルト設定でシミュレーターを初期化する。.</summary>
    public HardwareSimulator()
        : this(null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HardwareSimulator"/> class.設定プロバイダーを指定してシミュレーターを初期化する。.</summary>
    /// <param name="configProvider">設定プロバイダー。.</param>
    public HardwareSimulator(ConfigurationProvider? configProvider)
    {
        if (configProvider == null)
        {
            this.configProvider = new ConfigurationProvider();
            internalConfigProvider = this.configProvider;
        }
        else
        {
            this.configProvider = configProvider;
            internalConfigProvider = null;
        }
    }

    /// <inheritdoc/>
    public async Task SimulateDispenseAsync(CancellationToken ct = default)
    {
        var delay = configProvider?.Config?.Simulation?.DispenseDelayMs ?? 500;
        if (delay > 0)
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    /// <summary>リソースを破棄します。.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>マネージリソースを破棄します。.</summary>
    /// <param name="disposing">マネージリソースを破棄する場合は true。.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // internalConfigProvider が非 null の時のみ、自身で生成したインスタンスを破棄する
            // CA2213 遵守のため、configProvider フィールド経由での呼び出しを明示
            if (internalConfigProvider != null)
            {
                configProvider?.Dispose();
            }
        }
    }
}
