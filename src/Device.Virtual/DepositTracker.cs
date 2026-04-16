using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>投入された現金の追跡とバリデーションを担当するクラス。</summary>
internal sealed class DepositTracker(Inventory inventory, ConfigurationProvider configProvider)
{
    private readonly Inventory inventory = inventory;
    private readonly ConfigurationProvider configProvider = configProvider;

    /// <summary>投入された現金の追跡処理（集計、バリデーション、シリアル番号生成）を行います。</summary>
    /// <param name="key">投入された金種。</param>
    /// <param name="count">投入された枚数。</param>
    /// <param name="state">更新対象の預入状態。</param>
    public void ProcessDenominationTracking(DenominationKey key, int count, DepositState state)
    {
        // [UPOS] Simulate identification/validation
        state.Status = DeviceDepositStatus.Validation;

        var config = configProvider.Config;
        var denomConfig = config.GetDenominationSetting(key);
        var maxCount = denomConfig.Full;

        var currentInStorage = inventory.GetCount(key);
        var available = Math.Max(0, maxCount - currentInStorage);
        var overflow = Math.Max(0, count - available);

        // [LIFECYCLE] Record progress
        if (state.Counts.TryGetValue(key, out var current))
        {
            state.Counts[key] = current + count;
        }
        else
        {
            state.Counts[key] = count;
        }

        inventory.AddEscrow(key, count);
        state.DepositAmount += key.Value * count;
        state.OverflowAmount += key.Value * overflow;

        for (var i = 0; i < count; i++)
        {
            state.DepositedSerials.Add($"SN-{key.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{Guid.NewGuid().ToString()[..8]}");
        }

        // [LIFECYCLE] Finished identification
        state.Status = DeviceDepositStatus.Counting;
    }
}
