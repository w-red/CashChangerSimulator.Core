using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;

namespace CashChangerSimulator.Device;

/// <summary>釣銭機ハードウェアの動作をシミュレートするクラス。</summary>
public class HardwareSimulator : IDeviceSimulator
{
    private readonly ConfigurationProvider? _configProvider;
    private readonly ConfigurationProvider? _internalConfigProvider;

    /// <summary>デフォルト設定でシミュレーターを初期化する。</summary>
    public HardwareSimulator() : this(null) { }

    /// <summary>設定プロバイダーを指定してシミュレーターを初期化する。</summary>
    public HardwareSimulator(ConfigurationProvider? configProvider)
    {
        if (configProvider == null)
        {
            _configProvider = new ConfigurationProvider();
            _internalConfigProvider = _configProvider;
        }
        else
        {
            _configProvider = configProvider;
            _internalConfigProvider = null;
        }
    }

    /// <inheritdoc/>
    public async Task SimulateDispenseAsync(CancellationToken ct = default)
    {
        var delay = _configProvider?.Config?.Simulation?.DispenseDelayMs ?? 500;
        if (delay > 0)
        {
            await Task.Delay(delay, ct);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _internalConfigProvider?.Dispose();
        GC.SuppressFinalize(this);
    }
}
