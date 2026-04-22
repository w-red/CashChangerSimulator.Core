using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.Device.PosForDotNet.Facades;

/// <summary>デバイスの能力(CapXXX プロパティ)を公開する <see cref="CapabilitiesFacade"/>。</summary>
/// <param name="config">シミュレーターの設定情報である <see cref="SimulatorConfiguration"/>。</param>
/// <remarks>
/// UPOS 仕様に基づき、デバイスがサポートする機能(出金、入金、各ステータスの報告可否など)を読み取り専用で提供します。
/// </remarks>
public class CapabilitiesFacade(SimulatorConfiguration config)
{
    /// <inheritdoc/>
    public static bool CapDeposit => true;

    /// <inheritdoc/>
    public static bool CapDepositDataEvent => true;

    /// <inheritdoc/>
    public static bool CapPauseDeposit => true;

    /// <inheritdoc/>
    public static bool CapRepayDeposit => true;

    /// <inheritdoc/>
    public static bool CapPurgeCash => true;

    /// <inheritdoc/>
    public static bool CapDiscrepancy => true;

    /// <inheritdoc/>
    public static bool CapFullSensor => true;

    /// <inheritdoc/>
    public static bool CapNearFullSensor => true;

    /// <inheritdoc/>
    public static bool CapNearEmptySensor => true;

    /// <inheritdoc/>
    public static bool CapEmptySensor => true;

    /// <inheritdoc/>
    public static bool CapStatisticsReporting => true;

    /// <inheritdoc/>
    public static bool CapUpdateStatistics => true;

    /// <inheritdoc/>
    public bool CapRealTimeData => config.Simulation.CapRealTimeData;

    /// <inheritdoc/>
    public int CurrentExit { get; set; } = 1;

    /// <inheritdoc/>
    public static int DeviceExits => 1;
}
