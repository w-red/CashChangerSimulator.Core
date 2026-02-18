using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using R3;
using MoneyKind4Opos.Currencies.Interfaces;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device;

[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Virtual Cash Changer Simulator", 1, 14)]
public class SimulatorCashChanger : CashChangerBasic
{
    private readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly CashChangerManager _manager;
    private readonly OverallStatusAggregator _statusAggregator;
    private readonly IDisposable _statusSubscription;
    private readonly SimulatorConfiguration _config;

    private readonly DepositController _depositController;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly ILogger<SimulatorCashChanger> _logger;

    // Status tracking for StatusUpdateEvent transitions
    private Microsoft.PointOfService.CashChangerStatus _lastCashChangerStatus = CashChangerStatus.OK;
    private CashChangerFullStatus _lastFullStatus = CashChangerFullStatus.OK;

    // Async processing state
    private bool _asyncProcessing;
    private int _asyncResultCode;
    private int _asyncResultCodeExtended;

    internal Action<EventArgs>? OnEventQueued; // For testing

    protected virtual void NotifyEvent(EventArgs e)
    {
        OnEventQueued?.Invoke(e);
        if (e is DataEventArgs de) QueueEvent(de);
        else if (e is StatusUpdateEventArgs se) QueueEvent(se);
    }

    public SimulatorCashChanger() : this(null, null, null, null, null, null, null) { }

    internal SimulatorCashChanger(
        SimulatorConfiguration? config = null,
        Inventory? inventory = null,
        TransactionHistory? history = null,
        CashChangerManager? manager = null,
        DepositController? depositController = null,
        OverallStatusAggregator? aggregator = null,
        HardwareStatusManager? hardwareStatusManager = null)
    {
        // Load settings from TOML
        _config = config ?? ConfigurationLoader.Load();

        DevicePath = "SimulatorCashChanger";
        _hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();

        _logger = LogProvider.CreateLogger<SimulatorCashChanger>();
        _logger.ZLogInformation($"SimulatorCashChanger initialized.");

        _inventory = inventory ?? new Inventory();
        if (inventory == null)
        {
            var state = ConfigurationLoader.LoadInventoryState();
            if (state.Counts.Count > 0)
            {
                _inventory.LoadFromDictionary(state.Counts);
            }
            else
            {
                // Inventory から全ての通貨の金種をロード
                foreach (var currencyEntry in _config.Inventory)
                {
                    var currencyCode = currencyEntry.Key;
                    foreach (var item in currencyEntry.Value.Denominations)
                    {
                        if (DenominationKey.TryParse(item.Key, currencyCode, out var key) && key != null)
                        {
                            _inventory.SetCount(key, item.Value.InitialCount);
                        }
                    }
                }
            }
        }

        _history = history ?? new TransactionHistory();
        _manager = manager ?? new CashChangerManager(_inventory, _history);
        _depositController = depositController ?? new DepositController(_inventory, _manager, _config.Simulation, _hardwareStatusManager);

        // Status monitors / Aggregator
        var monitors = _inventory.AllCounts
            .Select(kv => (kv.Key, _config.GetDenominationSetting(kv.Key)))
            .Select(x => new CashStatusMonitor(_inventory, x.Key, x.Item2.NearEmpty, x.Item2.NearFull, x.Item2.Full))
            .ToList();
        _statusAggregator = aggregator ?? new OverallStatusAggregator(monitors);

        // Active currency initialization
        _activeCurrencyCode = _config.Inventory.Keys.FirstOrDefault() ?? "JPY";

        // Subscribe to status changes for StatusUpdateEvent
        _statusSubscription = Disposable.Combine(
            _statusAggregator.DeviceStatus.Subscribe(status => 
            {
                var newDeviceStatus = status switch
                {
                    CashStatus.Empty => CashChangerStatus.Empty,
                    CashStatus.NearEmpty => CashChangerStatus.NearEmpty,
                    _ => CashChangerStatus.OK
                };

                if (newDeviceStatus != _lastCashChangerStatus)
                {
                    _lastCashChangerStatus = newDeviceStatus;
                }
            }),
            _statusAggregator.FullStatus.Subscribe(status => 
            {
                var newFullStatus = status switch
                {
                    CashStatus.Full => CashChangerFullStatus.Full,
                    CashStatus.NearFull => CashChangerFullStatus.NearFull,
                    _ => CashChangerFullStatus.OK
                };

                if (newFullStatus != _lastFullStatus)
                {
                    _lastFullStatus = newFullStatus;
                    NotifyEvent(new StatusUpdateEventArgs((int)newFullStatus));
                }
            }),
            _hardwareStatusManager.IsJammed.Subscribe(jammed =>
            {
                if (jammed)
                {
                    _lastCashChangerStatus = CashChangerStatus.OK; // Property based status
                    NotifyEvent(new StatusUpdateEventArgs(205)); // CHAN_STATUS_JAM = 205
                }
                else
                {
                    _lastCashChangerStatus = CashChangerStatus.OK;
                    NotifyEvent(new StatusUpdateEventArgs(206)); // CHAN_STATUS_OK = 206
                }
            }),
            _hardwareStatusManager.IsOverlapped.Subscribe(overlapped =>
            {
                if (overlapped)
                {
                    _logger.ZLogWarning($"Device reported OVERLAP error.");
                    // There isn't a standard OPOS status for "Overlap" in CashChangerStatus enum, 
                    // but we can use StatusUpdateEvent with a custom code or just log it.
                    // For now, let's keep it as an internal state that blocks operations.
                }
            }),
            _depositController.Changed.Subscribe(_ =>
            {
                if (_depositController.DepositStatus == CashDepositStatus.Count && !_depositController.IsPaused && DataEventEnabled)
                {
                    NotifyEvent(new DataEventArgs(0));
                }
            })
        );
    }

    private string _activeCurrencyCode;

    public override string CheckHealth(HealthCheckLevel level) => "OK";
    public override string CheckHealthText => "OK";

    // ========== Deposit Methods (UPOS v1.5+) ==========

    public override void BeginDeposit() 
    {
        ThrowIfBusy();
        _depositController.BeginDeposit();
    }

    public override void EndDeposit(CashDepositAction action) => _depositController.EndDeposit(action);

    public override void FixDeposit() 
    {
        _depositController.FixDeposit();

        if (DataEventEnabled && CapDepositDataEvent)
        {
            QueueEvent(new DataEventArgs(0));
        }
    }

    public override void PauseDeposit(CashDepositPause control) => _depositController.PauseDeposit(control);

    // ========== Dispense Methods ==========

    private void ThrowIfDepositInProgress()
    {
        if (_depositController.IsDepositInProgress)
        {
            throw new PosControlException(
                "Cash cannot be dispensed because cash acceptance is in progress.",
                ErrorCode.Illegal);
        }
    }

    private void ThrowIfBusy()
    {
        if (_asyncProcessing)
        {
            throw new PosControlException("Device is busy with an asynchronous operation.", ErrorCode.Busy);
        }
    }

    private void StartAsyncDispense(Action action)
    {
        _asyncProcessing = true;
        _asyncResultCode = 0;
        _asyncResultCodeExtended = 0;

        Task.Run(async () => 
        {
            try
            {
                // Simulation: Delay
                if (_config.Simulation.DelayEnabled)
                {
                    var delay = Random.Shared.Next(_config.Simulation.MinDelayMs, _config.Simulation.MaxDelayMs);
                    await Task.Delay(delay);
                }

                // Simulation: Random Error
                if (_config.Simulation.RandomErrorsEnabled)
                {
                    var roll = Random.Shared.Next(0, 100);
                    if (roll < _config.Simulation.ErrorRate)
                    {
                        throw new PosControlException("Simulated Random Failure", ErrorCode.Failure);
                    }
                }

                action();
                _logger.ZLogInformation($"Async operation completed successfully.");
                _asyncResultCode = (int)ErrorCode.Success;
            }
            catch (PosControlException ex)
            {
                _logger.ZLogError(ex, $"Async operation failed: {ex.ErrorCode} ({ex.ErrorCodeExtended})");
                _asyncResultCode = (int)ex.ErrorCode;
                _asyncResultCodeExtended = ex.ErrorCodeExtended;
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"Async operation encountered an unexpected error.");
                _asyncResultCode = (int)ErrorCode.Failure;
            }
            finally
            {
                _asyncProcessing = false;
                NotifyEvent(new StatusUpdateEventArgs(201)); // CHAN_STATUS_ASYNC = 201
            }
        });
    }

    public override void DispenseChange(int amount)
    {
        if (amount <= 0) throw new PosControlException("Amount must be positive", ErrorCode.Illegal);

        ThrowIfDepositInProgress();
        ThrowIfBusy();
        if (_hardwareStatusManager.IsJammed.Value) throw new PosControlException("Device is jammed", ErrorCode.Failure);

        if (AsyncMode)
        {
            StartAsyncDispense(() => _manager.Dispense((decimal)amount));
        }
        else
        {
            try
            {
                _manager.Dispense((decimal)amount);
            }
            catch (InsufficientCashException ex)
            {
                throw new PosControlException(ex.Message, ErrorCode.Extended, 201); // ECHAN_OVERDISPENSE = 201
            }
        }
    }

    public override void DispenseCash(CashCount[] cashCounts) 
    {
        ThrowIfDepositInProgress();
        ThrowIfBusy();
        if (_hardwareStatusManager.IsJammed.Value) throw new PosControlException("Device is jammed", ErrorCode.Failure);

        void DoDispense()
        {
            foreach (var cc in cashCounts)
            {
                var cashType = (cc.Type == CashCountType.Bill) 
                    ? CashType.Bill 
                    : CashType.Coin;

                var factor = GetCurrencyFactor(_activeCurrencyCode);
                var val = cc.NominalValue / factor;
                var key = new DenominationKey(val, cashType, _activeCurrencyCode);
                
                if (_inventory.GetCount(key) < cc.Count)
                {
                    throw new PosControlException("Insufficient cash for denomination", ErrorCode.Extended, 201);
                }
                _inventory.Add(key, -cc.Count);
            }
        }

        if (AsyncMode)
        {
            StartAsyncDispense(DoDispense);
        }
        else
        {
            DoDispense();
        }
    }

    // ========== ReadCashCounts ==========
    
    public override CashCounts ReadCashCounts() 
    {
        ThrowIfBusy();
        var sorted = _inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .OrderBy(kv => kv.Key.Type) // Coin(0) before Bill(1)
            .ThenBy(kv => kv.Key.Value);

        var list = sorted.Select(kv => new CashCount(
            (kv.Key.Type == CashType.Bill) ? CashCountType.Bill : CashCountType.Coin,
            GetNominalValue(kv.Key),
            kv.Value)).ToList();

        return new CashCounts([.. list], false);
    }

    // ========== DirectIO ==========

    public override DirectIOData DirectIO(int command, int data, object obj) => new(data, obj);

    // ========== Status Properties ==========

    public override Microsoft.PointOfService.CashChangerStatus DeviceStatus => _lastCashChangerStatus;
    public override CashChangerFullStatus FullStatus => _lastFullStatus;

    // ========== Async Properties ==========

    public override bool AsyncMode { get; set; }
    public override int AsyncResultCode => _asyncResultCode;
    public override int AsyncResultCodeExtended => _asyncResultCodeExtended;

    // ========== Currency Properties ==========

    public override string CurrencyCode 
    { 
        get => _activeCurrencyCode; 
        set 
        {
            if (CurrencyCodeList.Contains(value))
            {
                _activeCurrencyCode = value;
            }
            else
            {
                throw new PosControlException($"Unsupported currency: {value}", ErrorCode.Illegal);
            }
        }
    }
    public override string[] CurrencyCodeList => [.. _config.Inventory.Keys.OrderBy(c => c)];
    public override string[] DepositCodeList => CurrencyCodeList;

    public override CashUnits CurrencyCashList => BuildCashUnits();
    public override CashUnits DepositCashList => CapDeposit ? BuildCashUnits() : new CashUnits();
    public override CashUnits ExitCashList => BuildCashUnits();

    // ========== Deposit Properties ==========

    public override int DepositAmount => (int)Math.Round(_depositController.DepositAmount * GetCurrencyFactor());
    public override CashCount[] DepositCounts 
    { 
        get => [.. _depositController.DepositCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .Select(kv => new CashCount(
                kv.Key.Type == CashType.Bill ? CashCountType.Bill : CashCountType.Coin,
                GetNominalValue(kv.Key),
                kv.Value))];
    }
    public override CashDepositStatus DepositStatus => _depositController.DepositStatus;
    
    // ========== Exit Properties ==========

    public override int CurrentExit { get => 1; set { } }
    public override int DeviceExits => 1;

    // ========== Capability Properties ==========

    public override bool CapDeposit => true;
    public override bool CapDepositDataEvent => true;
    public override bool CapPauseDeposit => true;
    public override bool CapRepayDeposit => false;

    public override bool CapDiscrepancy => false;
    public override bool CapFullSensor => true;
    public override bool CapNearFullSensor => true;
    public override bool CapNearEmptySensor => true;
    public override bool CapEmptySensor => true;

    // ========== Private Helpers ==========

    private CashUnits BuildCashUnits()
    {
        var activeUnits = _inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .OrderBy(kv => kv.Key.Value)
            .ToList();

        var coins = activeUnits
            .Where(kv => kv.Key.Type == CashType.Coin)
            .Select(kv => GetNominalValue(kv.Key))
            .ToArray();

        var bills = activeUnits
            .Where(kv => kv.Key.Type == CashType.Bill)
            .Select(kv => GetNominalValue(kv.Key))
            .ToArray();

        return new CashUnits(coins, bills);
    }

    private int GetNominalValue(DenominationKey key)
    {
        return (int)Math.Round(key.Value * GetCurrencyFactor(key.CurrencyCode));
    }

    private decimal GetCurrencyFactor(string? currencyCode = null)
    {
        var code = currencyCode ?? _activeCurrencyCode;
        return code switch
        {
            "USD" or "EUR" or "GBP" or "CAD" or "AUD" => 100m,
            _ => 1m
        };
    }
}
