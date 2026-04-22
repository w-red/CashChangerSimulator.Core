using System.Diagnostics.CodeAnalysis;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using PosSharp.Abstractions;
using ZLogger;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>
/// UPOS デバイスの標準的なライフサイクル(Open, Close, Claim, Release)を制御するハンドラー。
/// </summary>
public class StandardLifecycleHandler(
    HardwareStatusManager hardware,
    IUposMediator mediator,
    TransactionHistory history,
    ILogger logger) : IUposLifecycleHandler
{
    /// <inheritdoc/>
    public Microsoft.PointOfService.ControlState State
    {
        get
        {
            if (hardware.IsDisposed || !hardware.IsConnected.CurrentValue) return Microsoft.PointOfService.ControlState.Closed;
            if (mediator.IsBusy) return Microsoft.PointOfService.ControlState.Busy;
            return Microsoft.PointOfService.ControlState.Idle;
        }
    }

    /// <inheritdoc/>
    public bool Claimed => mediator.Claimed;

    /// <inheritdoc/>
    public bool DeviceEnabled
    {
        get => mediator.DeviceEnabled;
        set
        {
            if (value)
            {
                if (State == Microsoft.PointOfService.ControlState.Closed) throw new PosControlException("Device is closed.", ErrorCode.Closed);
                hardware.RefreshClaimedStatus();
                mediator.ClaimedByAnother = hardware.IsClaimedByAnother.CurrentValue;
                if (mediator.ClaimedByAnother) throw new PosControlException("Device is claimed by another application.", ErrorCode.Claimed);
                if (!Claimed) throw new PosControlException("Device is not claimed.", ErrorCode.NotClaimed);
                mediator.VerifyState(mustBeClaimed: true);
            }
            if (value == mediator.DeviceEnabled) return;
            mediator.DeviceEnabled = value;
            logger?.ZLogInformation($"DeviceEnabled set to {value}.");
        }
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
        if (State != Microsoft.PointOfService.ControlState.Closed)
        {
            mediator.SetSuccess();
            return;
        }
        try { baseOpen(); } catch (Exception ex) { logger?.LogWarning(ex, "Open() failed."); }
        hardware.Input.IsConnected.Value = true;
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Open, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Close(Action baseClose)
    {
        ArgumentNullException.ThrowIfNull(baseClose);
        if (State == Microsoft.PointOfService.ControlState.Closed)
        {
            mediator.SetSuccess();
            return;
        }
        if (DeviceEnabled)
        {
            try { DeviceEnabled = false; }
            catch (Exception ex) { logger?.LogWarning(ex, "Implicit disable failed during Close."); }
        }
        try { baseClose(); } catch (Exception ex) { logger?.LogWarning(ex, "Close() failed."); }
        if (Claimed) history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Release, 0, new Dictionary<DenominationKey, int>()));
        if (hardware is { IsDisposed: false, Input: not null }) hardware.Input.IsConnected.Value = false;
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Close, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }

    /// <inheritdoc/>
    [SuppressMessage("SonarAnalyzer.CSharp", "S1696:NullReferenceException should not be caught", Justification = "SDK internal bug causes NRE in Claim() when registry is missing.")]
    public void Claim(int timeout, Action<int> baseClaim)
    {
        ArgumentNullException.ThrowIfNull(baseClaim);
        if (State == Microsoft.PointOfService.ControlState.Closed) throw new PosControlException("Device is closed.", ErrorCode.Closed);
        if (Claimed) { mediator.SetSuccess(); return; }
        try
        {
            if (!hardware.TryAcquireGlobalLock()) throw new PosControlException("Device is claimed by another application.", ErrorCode.Claimed);
            baseClaim(timeout);
        }
        catch (NullReferenceException nre)
        {
            // POS for .NET SDK internal NRE is unavoidable in some environments (e.g. missing registry).
            // We MUST catch this specifically to allow the simulator to proceed.
            logger?.LogWarning(nre, "POS for .NET SDK internal Claim caused NRE. This is expected in environments without full POS setup.");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Claim() failed.");
        }
        mediator.Claimed = true;
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Claim, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Release(Action baseRelease)
    {
        ArgumentNullException.ThrowIfNull(baseRelease);
        if (State == Microsoft.PointOfService.ControlState.Closed) throw new PosControlException("Device is closed.", ErrorCode.Closed);
        if (!Claimed) { mediator.SetSuccess(); return; }
        try { baseRelease(); } catch (Exception ex) { logger?.LogWarning(ex, "Release() failed."); }
        mediator.Claimed = false;
        hardware.ReleaseGlobalLock();
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Release, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }
}
