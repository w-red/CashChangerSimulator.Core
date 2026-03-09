using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>標準的な UPOS ライフサイクル（状態検証あり）を実装するクラス。</summary>
public class StandardLifecycleHandler : IUposLifecycleHandler
{
    private readonly HardwareStatusManager _hardware;
    private readonly IUposMediator _mediator;
    private readonly TransactionHistory _history;
    private readonly ILogger _logger;

    public StandardLifecycleHandler(HardwareStatusManager hardware, IUposMediator mediator, TransactionHistory history, ILogger logger)
    {
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ControlState State
    {
        get
        {
            if (!_hardware.IsConnected.Value) return ControlState.Closed;
            if (_mediator.IsBusy) return ControlState.Busy;
            return ControlState.Idle;
        }
    }

    public bool Claimed => _mediator.Claimed;
    public bool DeviceEnabled
    {
        get => _mediator.DeviceEnabled;
        set
        {
            if (value)
            {
                if (State == ControlState.Closed)
                {
                    throw new PosControlException("Device is not open.", ErrorCode.Closed);
                }

                if (!Claimed)
                {
                    throw new PosControlException("Device must be claimed before enabling.", ErrorCode.Illegal);
                }
            }

            if (value == _mediator.DeviceEnabled) return;

            _mediator.DeviceEnabled = value;
            _logger.LogInformation("DeviceEnabled set to {0}.", value);
        }
    }
    public bool DataEventEnabled
    {
        get => _mediator.DataEventEnabled;
        set => _mediator.DataEventEnabled = value;
    }

    public void Open(Action baseOpen)
    {
        if (baseOpen == null) throw new ArgumentNullException(nameof(baseOpen));
        if (_logger == null) throw new InvalidOperationException("_logger is null in StandardLifecycleHandler.Open");

        if (State != ControlState.Closed)
        {
            _logger.LogInformation("Open called but device is already {0}.", State);
            _mediator.SetSuccess();
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
            _logger.LogWarning(ex, "base.Open() failed (likely due to missing registry info). Ignoring to allow simulation.");
        }

        _hardware.SetConnected(true);
        _history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Open, 0, new Dictionary<DenominationKey, int>()));
        _mediator.SetSuccess();
    }

    public void Close(Action baseClose)
    {
        if (baseClose == null) throw new ArgumentNullException(nameof(baseClose));
        if (State == ControlState.Closed)
        {
            _logger.LogInformation("Close called but device is already Closed.");
            _mediator.SetSuccess();
            return;
        }

        try
        {
            baseClose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "base.Close() failed. Ignoring.");
        }

        _hardware.SetConnected(false);
        _history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Close, 0, new Dictionary<DenominationKey, int>()));
        _mediator.SetSuccess();
    }

    public void Claim(int timeout, Action<int> baseClaim)
    {
        if (baseClaim == null) throw new ArgumentNullException(nameof(baseClaim));

        if (State == ControlState.Closed)
        {
            _logger.LogWarning("Claim called while device is Closed.");
            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        if (Claimed)
        {
            _logger.LogInformation("Claim called but device is already claimed.");
            _mediator.SetSuccess();
            return;
        }

        try
        {
            baseClaim(timeout);
        }
        catch (Exception ex)
        {
            // POS for .NET often throws NRE if internal state is not perfect.
            _logger.LogWarning(ex, "base.Claim({0}) failed. Ignoring to allow simulation.", timeout);
        }

        _mediator.Claimed = true;
        _history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Claim, 0, new Dictionary<DenominationKey, int>()));
        _mediator.SetSuccess();
    }

    public void Release(Action baseRelease)
    {
        if (baseRelease == null) throw new ArgumentNullException(nameof(baseRelease));

        if (State == ControlState.Closed)
        {
            _logger.LogWarning("Release called while device is Closed.");
            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        if (!Claimed)
        {
            _logger.LogInformation("Release called but device is not claimed.");
            _mediator.SetSuccess();
            return;
        }

        try
        {
            baseRelease();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "base.Release() failed. Ignoring.");
        }

        _mediator.Claimed = false;
        _history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Release, 0, new Dictionary<DenominationKey, int>()));
        _mediator.SetSuccess();
    }
}
