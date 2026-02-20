using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device;

/// <summary>シミュレーション固有の振る舞い（遅延・ランダムエラー）を提供するヘルパー。</summary>
public static class SimulationBehavior
{
    /// <summary>設定に基づくランダム遅延を実行します。</summary>
    public static async Task ApplyDelayAsync(SimulationSettings config)
    {
        if (!config.DelayEnabled) return;

        var delay = Random.Shared.Next(config.MinDelayMs, config.MaxDelayMs);
        await Task.Delay(delay);
    }

    /// <summary>設定に基づくランダムエラーを発生させます。</summary>
    /// <exception cref="PosControlException">ランダムエラー発生時。</exception>
    public static void ThrowIfRandomError(SimulationSettings config)
    {
        if (!config.RandomErrorsEnabled) return;

        var roll = Random.Shared.Next(0, 100);
        if (roll < config.ErrorRate)
        {
            throw new PosControlException("Simulated Random Failure", ErrorCode.Failure);
        }
    }
}
