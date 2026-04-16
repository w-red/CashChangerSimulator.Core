using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using ZLogger;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>標準的な UPOS ライフサイクル(状態検証あり)を実装するクラス。</summary>
public class StandardLifecycleHandler(
    HardwareStatusManager hardware,
    IUposMediator mediator,
    TransactionHistory history,
    ILogger logger) : IUposLifecycleHandler
{
    /// <inheritdoc/>
    public ControlState State
    {
        get
        {
            if (hardware.IsDisposed || !hardware.IsConnected.CurrentValue)
            {
                return ControlState.Closed;
            }

            if (mediator.IsBusy)
            {
                return ControlState.Busy;
            }

            return ControlState.Idle;
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
                // [FIX] Perform minimal local checks to satisfy unit tests that use a mock mediator.
                // [修正] Mock メディエーターを使用するユニットテストを満たすため、最小限のローカルチェックを実行します。
                if (State == ControlState.Closed)
                {
                    throw new PosControlException("Device is closed.", ErrorCode.Closed);
                }

                // [FIX] Always refresh the global lock status before checking ClaimedByAnother for precedence.
                // [修正] 優先順位の検証前に、グローバルロックの状態を常に最新化します。
                hardware.RefreshClaimedStatus();
                mediator.ClaimedByAnother = hardware.IsClaimedByAnother.CurrentValue;

                if (mediator.ClaimedByAnother)
                {
                    throw new PosControlException("Device is claimed by another application.", ErrorCode.Claimed);
                }

                if (!Claimed)
                {
                    throw new PosControlException("Device is not claimed.", ErrorCode.NotClaimed);
                }

                // [UPOS PRECEDENCE] Use mediator for centralized complex verification.
                // [UPOS 優先順位] 集中管理された複雑な検証にはメディエーターを使用します。
                mediator.VerifyState(mustBeClaimed: true);
            }

            if (value == mediator.DeviceEnabled)
            {
                return;
            }

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

        if (State != ControlState.Closed)
        {
            logger?.ZLogInformation($"Open called but device is already {State}.");

            mediator.SetSuccess();
            return;
        }

        try
        {
            baseOpen();
        }
        catch (Exception ex)
        {
            // POS for .NET often throws NRE or PosControlException when registry entries are missing.
            // We ignore these in the simulator's standard handler to allow testing logic without a full installation.
            logger?.LogWarning(ex, "POS for .NET internal Open() failed. This is expected in environments without full POS setup.");
        }

        hardware.Input.IsConnected.Value = true;
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Open, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Close(Action baseClose)
    {
        ArgumentNullException.ThrowIfNull(baseClose);

        if (State == ControlState.Closed)
        {
            logger?.ZLogInformation($"Close called but device is already Closed.");

            mediator.SetSuccess();
            return;
        }

        // [SAFE SEQUENCE] Disable -> Release -> Close to prevent POS for .NET internal NRE on exit.
        if (DeviceEnabled)
        {
            try
            {
                DeviceEnabled = false;
            }
            catch
            {
            }
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
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "POS for .NET internal Close() failed (non-critical).");
        }

        if (Claimed)
        {
            logger?.ZLogInformation($"Close called while device is Claimed. Adding implicit Release log.");

            history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Release, 0, new Dictionary<DenominationKey, int>()));
        }

        if (hardware is { IsDisposed: false, Input: not null })
        {
            hardware.Input.IsConnected.Value = false;
        }

        history?.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Close, 0, new Dictionary<DenominationKey, int>()));
        mediator?.SetSuccess();
    }

    /// <inheritdoc/>
    public void Claim(int timeout, Action<int> baseClaim)
    {
        ArgumentNullException.ThrowIfNull(baseClaim);

        if (State == ControlState.Closed)
        {
            logger?.LogWarning("Claim called while device is Closed.");

            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        if (Claimed)
        {
            logger?.LogInformation("Claim called but device is already claimed.");

            mediator.SetSuccess();
            return;
        }

        try
        {
            if (!hardware.TryAcquireGlobalLock())
            {
                logger?.LogWarning("Claim failed due to global lock (claimed by another process).");

                throw new PosControlException("Device is claimed by another application.", ErrorCode.Claimed);
            }

            baseClaim(timeout);
        }
        catch (Exception ex)
        {
            // POS for .NET often throws NRE if internal state is not perfect (e.g. missing registry).
            // We MUST catch this to allow the simulator to proceed in a standalone/test environment.
            logger?.LogWarning(ex, "POS for .NET internal Claim({0}) failed. This is expected in environments without full POS setup.", timeout);
        }

        mediator.Claimed = true;
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Claim, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Release(Action baseRelease)
    {
        ArgumentNullException.ThrowIfNull(baseRelease);

        if (State == ControlState.Closed)
        {
            logger?.LogWarning("Release called while device is Closed.");

            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        if (!Claimed)
        {
            logger?.LogInformation("Release called but device is not claimed.");

            mediator.SetSuccess();
            return;
        }

        try
        {
            baseRelease();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "POS for .NET internal Release() failed (non-critical).");
        }

        mediator.Claimed = false;
        hardware.ReleaseGlobalLock();
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Release, 0, new Dictionary<DenominationKey, int>()));
        mediator.SetSuccess();
    }
}
