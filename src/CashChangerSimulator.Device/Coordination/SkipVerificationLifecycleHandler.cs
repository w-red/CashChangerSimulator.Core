using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>検証をスキップするシミュレータ用の UPOS ライフサイクルを実装するクラス。</summary>
public class SkipVerificationLifecycleHandler(HardwareStatusManager hardware, IUposMediator mediator, TransactionHistory history, ILogger logger) : IUposLifecycleHandler
{
    private readonly HardwareStatusManager _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
    private readonly IUposMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    private readonly TransactionHistory _history = history ?? throw new ArgumentNullException(nameof(history));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public ControlState State
    {
        get
        {
            return !_hardware.IsConnected.Value
                ? ControlState.Closed
                : _mediator.IsBusy
                    ? ControlState.Busy
                    : ControlState.Idle;
        }
    }

    public bool Claimed => _mediator.Claimed;
    public bool DeviceEnabled
    {
        get => _mediator.DeviceEnabled;
        set => _mediator.DeviceEnabled = value;
    }
    public bool DataEventEnabled
    {
        get => _mediator.DataEventEnabled;
        set => _mediator.DataEventEnabled = value;
    }

    /// <inheritdoc/>
    public void Open(Action baseOpen)
    {
        if (baseOpen == null) throw new ArgumentNullException(nameof(baseOpen));
        // Skip baseOpen()
        _hardware.SetConnected(true);
        _history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Open, 0, new Dictionary<DenominationKey, int>()));
        _logger.LogInformation("Device opened (Verification Skipped).");
        _mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Close(Action baseClose)
    {
        if (baseClose == null) throw new ArgumentNullException(nameof(baseClose));
        _hardware.SetConnected(false);
        _history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Close, 0, new Dictionary<DenominationKey, int>()));
        _logger.LogInformation("Device closed (Verification Skipped).");
        _mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Claim(int timeout, Action<int> baseClaim)
    {
        if (baseClaim == null) throw new ArgumentNullException(nameof(baseClaim));
        
        if (State == ControlState.Closed)
        {
            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        _mediator.Claimed = true;
        _history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Claim, 0, new Dictionary<DenominationKey, int>()));
        _logger.LogInformation("Device claimed (Verification Skipped).");
        _mediator.SetSuccess();
    }

    /// <inheritdoc/>
    public void Release(Action baseRelease)
    {
        if (baseRelease == null) throw new ArgumentNullException(nameof(baseRelease));

        if (State == ControlState.Closed)
        {
            // Usually no-op or throw. For parity:
            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        _mediator.Claimed = false;
        _history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Release, 0, new Dictionary<DenominationKey, int>()));
        _logger.LogInformation("Device released (Verification Skipped).");
        _mediator.SetSuccess();
    }
}
