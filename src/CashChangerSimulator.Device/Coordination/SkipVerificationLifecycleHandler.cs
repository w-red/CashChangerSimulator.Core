using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>検証をスキップするシミュレータ用の UPOS ライフサイクルを実装するクラス。</summary>
public class SkipVerificationLifecycleHandler(HardwareStatusManager hardware, IUposMediator mediator, TransactionHistory history, ILogger logger) : IUposLifecycleHandler
{
    public ControlState State => !hardware.IsConnected.Value ? ControlState.Closed : mediator.IsBusy ? ControlState.Busy : ControlState.Idle;

    public bool Claimed => mediator.Claimed;

    public bool DeviceEnabled
    {
        get => mediator.DeviceEnabled;
        set => mediator.DeviceEnabled = value;
    }

    public bool DataEventEnabled
    {
        get => mediator.DataEventEnabled;
        set => mediator.DataEventEnabled = value;
    }

    /// <inheritdoc/>
    public void Open(Action baseOpen)
    {
        ArgumentNullException.ThrowIfNull(baseOpen);
        // Skip baseOpen()
        hardware.SetConnected(true);
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Open, 0, new Dictionary<DenominationKey, int>()));
        logger.LogInformation("Device opened (Verification Skipped).");
        mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Close(Action baseClose)
    {
        ArgumentNullException.ThrowIfNull(baseClose);
        hardware.SetConnected(false);
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Close, 0, new Dictionary<DenominationKey, int>()));
        logger.LogInformation("Device closed (Verification Skipped).");
        mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Claim(int timeout, Action<int> baseClaim)
    {
        ArgumentNullException.ThrowIfNull(baseClaim);
        
        if (State == ControlState.Closed)
        {
            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        mediator.Claimed = true;
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Claim, 0, new Dictionary<DenominationKey, int>()));
        logger.LogInformation("Device claimed (Verification Skipped).");
        mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Release(Action baseRelease)
    {
        ArgumentNullException.ThrowIfNull(baseRelease);

        if (State == ControlState.Closed)
        {
            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        mediator.Claimed = false;
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Release, 0, new Dictionary<DenominationKey, int>()));
        logger.LogInformation("Device released (Verification Skipped).");
        mediator.SetSuccess();
    }
}
