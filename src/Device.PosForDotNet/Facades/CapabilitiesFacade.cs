using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.Device.PosForDotNet.Facades;

/// <summary>デバイスの能力（CapXXX プロパティ）を公開する <see cref="CapabilitiesFacade"/>。.</summary>
/// <param name="config">シミュレーターの設定情報である <see cref="SimulatorConfiguration"/>。.</param>
/// <remarks>
/// UPOS 仕様に基づき、デバイスがサポートする機能（出金、入金、各ステータスの報告可否など）を読み取り専用で提供します。.
/// </remarks>
public class CapabilitiesFacade(SimulatorConfiguration config)
{
    /// <inheritdoc/>
    public bool CapDeposit => true;
    /// <inheritdoc/>
    public bool CapDepositDataEvent => true;
    /// <inheritdoc/>
    public bool CapPauseDeposit => true;
    /// <inheritdoc/>
    public bool CapRepayDeposit => true;
    /// <inheritdoc/>
    public bool CapPurgeCash => true;

    /// <inheritdoc/>
    public bool CapDiscrepancy => true;
    /// <inheritdoc/>
    public bool CapFullSensor => true;
    /// <inheritdoc/>
    public bool CapNearFullSensor => true;
    /// <inheritdoc/>
    public bool CapNearEmptySensor => true;
    /// <inheritdoc/>
    public bool CapEmptySensor => true;

    /// <inheritdoc/>
    public bool CapStatisticsReporting => true;
    /// <inheritdoc/>
    public bool CapUpdateStatistics => true;

    /// <inheritdoc/>
    public bool CapRealTimeData => config.Simulation.CapRealTimeData;

    /// <inheritdoc/>
    public int CurrentExit { get; set; } = 1;
    /// <inheritdoc/>
    public int DeviceExits => 1;
}
