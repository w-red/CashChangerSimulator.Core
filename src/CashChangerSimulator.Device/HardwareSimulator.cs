using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;

namespace CashChangerSimulator.Device;

/// <summary>釣銭機ハードウェアの動作をシミュレートするクラス。</summary>
/// <param name="configProvider">設定プロバイダー。</param>
public class HardwareSimulator(ConfigurationProvider configProvider) : IDeviceSimulator
{
    /// <inheritdoc/>
    public async Task SimulateDispenseAsync(CancellationToken ct = default)
    {
        var delay = configProvider?.Config?.Simulation?.DispenseDelayMs ?? 500;
        if (delay > 0)
        {
            await Task.Delay(delay, ct);
        }
    }
}
