using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>UPOS ライフサイクル（Open, Close, Claim, Release）と基本状態の管理を担当するインターフェース。</summary>
public interface IUposLifecycleHandler
{
    ControlState State { get; }
    bool Claimed { get; }
    bool DeviceEnabled { get; set; }
    bool DataEventEnabled { get; set; }

    void Open(Action baseOpen);
    void Close(Action baseClose);
    void Claim(int timeout, Action<int> baseClaim);
    void Release(Action baseRelease);
}
