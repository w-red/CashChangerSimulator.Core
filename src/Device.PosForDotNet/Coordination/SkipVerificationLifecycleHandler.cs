using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using ZLogger;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>検証をスキップするシミュレータ用の UPOS ライフサイクルを実装するクラス。</summary>
public class SkipVerificationLifecycleHandler(
    HardwareStatusManager hardware,
    IUposMediator mediator,
    TransactionHistory history,
    ILogger logger)
    : IUposLifecycleHandler
{
    /// <inheritdoc/>
    public ControlState State
    {
        get
        {
            if (!hardware.IsConnected.CurrentValue)
            {
                return ControlState.Closed;
            }

            return mediator.IsBusy ? ControlState.Busy : ControlState.Idle;
        }
    }

    /// <inheritdoc/>
    public bool Claimed => mediator.Claimed;

    /// <inheritdoc/>
    public bool DeviceEnabled
    {
        get => mediator.DeviceEnabled;
        set => mediator.DeviceEnabled = value;
    }

    /// <inheritdoc/>
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
        hardware.Input.IsConnected.Value = true;
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Open, 0, new Dictionary<DenominationKey, int>()));
        logger.ZLogInformation($"Device opened (Verification Skipped).");
        mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Close(Action baseClose)
    {
        ArgumentNullException.ThrowIfNull(baseClose);
        if (Claimed)
        {
            history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Release, 0, new Dictionary<DenominationKey, int>()));
        }

        if (hardware is { IsDisposed: false, Input: not null })
        {
            hardware.Input.IsConnected.Value = false;
        }

        history?.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Close, 0, new Dictionary<DenominationKey, int>()));
        logger?.ZLogInformation($"Device closed (Verification Skipped).");
        mediator?.SetSuccess();
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
        mediator.ClaimedByAnother = hardware.IsClaimedByAnother.CurrentValue;
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Claim, 0, new Dictionary<DenominationKey, int>()));
        logger.ZLogInformation($"Device claimed (Verification Skipped).");
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
        logger.ZLogInformation($"Device released (Verification Skipped).");
        mediator.SetSuccess();
    }
}
