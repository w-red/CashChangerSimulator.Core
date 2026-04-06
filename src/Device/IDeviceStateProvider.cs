namespace CashChangerSimulator.Device;

/// <summary>デバイスの現在の制御状態を提供するインターフェース。</summary>
/// <remarks>設定マネージャーなどが、デバイスのオープン状態に応じて動作を切り替えるために使用します。</remarks>
public interface IDeviceStateProvider
{
    /// <summary>Gets デバイスの現在の制御状態を取得します。</summary>
    DeviceControlState State { get; }
}
