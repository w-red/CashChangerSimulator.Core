using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.PosForDotNet.Lifecycle;

/// <summary>デバイスのライフサイクル状態遷移に必要なコンテキスト情報。.</summary>
public class DeviceLifecycleContext(
    HardwareStatusManager hardwareStatusManager,
    ILogger logger,
    Action<bool> setDeviceEnabled)
{
    /// <summary>Gets ハードウェアステータスマネージャー。.</summary>
    public HardwareStatusManager HardwareStatusManager => hardwareStatusManager;

    /// <summary>Gets ロガー。.</summary>
    public ILogger Logger => logger;

    /// <summary>Gets deviceEnabled プロパティを設定するデリゲート。.</summary>
    public Action<bool> SetDeviceEnabled => setDeviceEnabled;
}
