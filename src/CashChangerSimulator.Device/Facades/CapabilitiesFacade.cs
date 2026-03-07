using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.Device.Facades;

/// <summary>デバイスの能力（CapXXX プロパティ）を公開する <see cref="CapabilitiesFacade"/>。</summary>
/// <param name="config">シミュレーターの設定情報である <see cref="SimulatorConfiguration"/>。</param>
/// <remarks>
/// UPOS 仕様に基づき、デバイスがサポートする機能（出金、入金、各ステータスの報告可否など）を読み取り専用で提供します。
/// </remarks>
public class CapabilitiesFacade(SimulatorConfiguration config)
{
    public bool CapDeposit => true;
    public bool CapDepositDataEvent => true;
    public bool CapPauseDeposit => true;
    public bool CapRepayDeposit => true;
    public bool CapPurgeCash => true;

    public bool CapDiscrepancy => true;
    public bool CapFullSensor => true;
    public bool CapNearFullSensor => true;
    public bool CapNearEmptySensor => true;
    public bool CapEmptySensor => true;

    public bool CapStatisticsReporting => true;
    public bool CapUpdateStatistics => true;

    public bool CapRealTimeData => config.Simulation.CapRealTimeData;

    public int CurrentExit { get; set; } = 1;
    public int DeviceExits => 1;
}
