using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.PosForDotNet.Lifecycle;

/// <summary>デバイスのライフサイクル状態遷移に必要なコンテキスト情報。</summary>
public class DeviceLifecycleContext(
    HardwareStatusManager hardwareStatusManager,
    ILogger logger,
    Action<bool> setDeviceEnabled)
{
    /// <summary>ハードウェアステータスマネージャーを取得します。</summary>
    public HardwareStatusManager HardwareStatusManager => hardwareStatusManager;

    /// <summary>ロガーを取得します。</summary>
    public ILogger Logger => logger;

    /// <summary>deviceEnabled プロパティを設定するデリゲートを取得します。</summary>
    public Action<bool> SetDeviceEnabled => setDeviceEnabled;
}
