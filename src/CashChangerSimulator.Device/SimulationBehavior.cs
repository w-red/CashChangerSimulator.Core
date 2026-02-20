using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.Device;

/// <summary>シミュレーション固有の振る舞い（遅延）を提供するヘルパー。</summary>
public static class SimulationBehavior
{
    /// <summary>設定に基づくランダム遅延を実行します。</summary>
    public static async Task ApplyDelayAsync(SimulationSettings config)
    {
        if (!config.DelayEnabled) return;

        var delay = Random.Shared.Next(config.MinDelayMs, config.MaxDelayMs);
        await Task.Delay(delay);
    }
}
