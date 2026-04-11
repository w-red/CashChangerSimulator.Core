using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>UPOS ライフサイクル(Open, Close, Claim, Release)と基本状態の管理を担当するインターフェース。</summary>
public interface IUposLifecycleHandler
{
    /// <inheritdoc/>
    ControlState State { get; }

    /// <inheritdoc/>
    bool Claimed { get; }

    /// <inheritdoc/>
    bool DeviceEnabled { get; set; }

    /// <inheritdoc/>
    bool DataEventEnabled { get; set; }

    /// <inheritdoc/>
    void Open(Action baseOpen);

    /// <inheritdoc/>
    void Close(Action baseClose);

    /// <inheritdoc/>
    void Claim(int timeout, Action<int> baseClaim);

    /// <inheritdoc/>
    void Release(Action baseRelease);
}
