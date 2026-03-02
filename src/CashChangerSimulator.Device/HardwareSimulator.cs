using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using MicroResolver;

namespace CashChangerSimulator.Device;

/// <summary>釣銭機ハードウェアの動作をシミュレートするクラス。</summary>
public class HardwareSimulator : IDeviceSimulator
{
    private readonly ConfigurationProvider? _configProvider;

    /// <summary>デフォルト設定でシミュレーターを初期化する。</summary>
    [Inject]
    public HardwareSimulator() : this(null) { }

    /// <summary>設定プロバイダーを指定してシミュレーターを初期化する。</summary>
    public HardwareSimulator(ConfigurationProvider? configProvider)
    {
        _configProvider = configProvider;
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
}
