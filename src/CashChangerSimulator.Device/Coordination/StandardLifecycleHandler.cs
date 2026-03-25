using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using ZLogger;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>標準的な UPOS ライフサイクル（状態検証あり）を実装するクラス。</summary>
public class StandardLifecycleHandler(
    HardwareStatusManager hardware,
    IUposMediator mediator,
    TransactionHistory history,
    ILogger logger) : IUposLifecycleHandler
{
    public ControlState State
    {
        get
        {
            if (hardware.IsDisposed || !hardware.IsConnected.Value) return ControlState.Closed;
            if (mediator.IsBusy) return ControlState.Busy;
            return ControlState.Idle;
        }
    }

    public bool Claimed => mediator.Claimed;

    public bool DeviceEnabled
    {
        get => mediator.DeviceEnabled;
        set
        {
            if (value)
            {
                if (State == ControlState.Closed)
                {
                    throw new PosControlException("Device is not open.", ErrorCode.Closed);
                }

                if (hardware.IsClaimedByAnother.Value)
                {
                    throw new PosControlException("Device is claimed by another application.", ErrorCode.Claimed);
                }

                if (!Claimed)
                {
                    throw new PosControlException("Device must be claimed before enabling.", ErrorCode.NotClaimed);
                }
            }

            if (value == mediator.DeviceEnabled) return;

            mediator.DeviceEnabled = value;
            if (logger != null)
            {
                logger.ZLogInformation($"DeviceEnabled set to {value}.");
            }
        }
    }

    public bool DataEventEnabled
    {
        get => mediator.DataEventEnabled;
        set => mediator.DataEventEnabled = value;
    }

    public void Open(Action baseOpen)
    {
        ArgumentNullException.ThrowIfNull(baseOpen);

        if (State != ControlState.Closed)
        {
            if (logger != null)
            {
                logger.ZLogInformation($"Open called but device is already {State}.");
            }
            mediator.SetSuccess();
            return;
        }

        try
        {
            baseOpen();
        }
        catch (System.Exception ex)
        {
            // POS for .NET often throws NRE or PosControlException when registry entries are missing.
            // We ignore these in the simulator's standard handler to allow testing logic without a full installation.
            if (logger != null)
            {
                logger.LogWarning(ex, "POS for .NET internal Open() failed. This is expected in environments without full POS setup.");
            }
        }

        hardware.SetConnected(true);
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Open, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }

    public void Close(Action baseClose)
    {
        ArgumentNullException.ThrowIfNull(baseClose);

        if (State == ControlState.Closed)
        {
            if (logger != null)
            {
                logger.ZLogInformation($"Close called but device is already Closed.");
            }
            mediator.SetSuccess();
            return;
        }

        // [SAFE SEQUENCE] Disable -> Release -> Close to prevent POS for .NET internal NRE on exit.
        if (DeviceEnabled)
        {
            try { DeviceEnabled = false; } catch { }
        }

        if (Claimed)
        {
            // Note: We don't have the baseRelease delegate here, 
            // but setting Claimed=false and adding history is handled below.
            // Most SDKs handle the internal release during Close() if disabled.
        }

        try
        {
            baseClose();
        }
        catch (System.Exception ex)
        {
            if (logger != null)
            {
                logger.LogWarning(ex, "POS for .NET internal Close() failed (non-critical).");
            }
        }

        if (Claimed)
        {
            if (logger != null)
            {
                logger.ZLogInformation($"Close called while device is Claimed. Adding implicit Release log.");
            }
            history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Release, 0, new Dictionary<DenominationKey, int>()));
        }

        hardware.SetConnected(false);
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Close, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }

    public void Claim(int timeout, Action<int> baseClaim)
    {
        ArgumentNullException.ThrowIfNull(baseClaim);

        if (State == ControlState.Closed)
        {
            if (logger != null)
            {
                logger.LogWarning("Claim called while device is Closed.");
            }
            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        if (Claimed)
        {
            if (logger != null)
            {
                logger.LogInformation("Claim called but device is already claimed.");
            }
            mediator.SetSuccess();
            return;
        }

        try
        {
            if (!hardware.TryAcquireGlobalLock())
            {
                if (logger != null)
                {
                    logger.LogWarning("Claim failed due to global lock (claimed by another process).");
                }
                throw new PosControlException("Device is claimed by another application.", ErrorCode.Claimed);
            }
            baseClaim(timeout);
        }
        catch (System.Exception ex)
        {
            // POS for .NET often throws NRE if internal state is not perfect (e.g. missing registry).
            // We MUST catch this to allow the simulator to proceed in a standalone/test environment.
            if (logger != null)
            {
                logger.LogWarning(ex, "POS for .NET internal Claim({0}) failed. This is expected in environments without full POS setup.", timeout);
            }
        }

        mediator.Claimed = true;
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Claim, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }

    public void Release(Action baseRelease)
    {
        ArgumentNullException.ThrowIfNull(baseRelease);

        if (State == ControlState.Closed)
        {
            if (logger != null)
            {
                logger.LogWarning("Release called while device is Closed.");
            }
            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        if (!Claimed)
        {
            if (logger != null)
            {
                logger.LogInformation("Release called but device is not claimed.");
            }
            mediator.SetSuccess();
            return;
        }

        try
        {
            baseRelease();
        }
        catch (System.Exception ex)
        {
            if (logger != null)
            {
                logger.LogWarning(ex, "POS for .NET internal Release() failed (non-critical).");
            }
        }

        mediator.Claimed = false;
        hardware.ReleaseGlobalLock();
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Release, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }
}
