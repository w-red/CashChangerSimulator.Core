using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Configuration;
using R3;
using MoneyKind4Opos.Currencies.Interfaces;

namespace CashChangerSimulator.Device;

[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Virtual Cash Changer Simulator", 1, 14)]
public class SimulatorCashChanger : CashChangerBasic
{
    private readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly CashChangerManager _manager;
    private readonly OverallStatusAggregator _statusAggregator;
    private readonly IDisposable _statusSubscription;
    private int _depositAmount;
    private readonly Dictionary<DenominationKey, int> _depositCounts = [];
    private CashDepositStatus _depositStatus = CashDepositStatus.None;
    private Microsoft.PointOfService.CashChangerStatus _lastCashChangerStatus = Microsoft.PointOfService.CashChangerStatus.OK;

    public SimulatorCashChanger()
    {
        // Load settings from TOML
        var config = ConfigurationLoader.Load();

        _inventory = new Inventory();
        var state = ConfigurationLoader.LoadInventoryState();
        if (state.Counts.Count > 0)
        {
            _inventory.LoadFromDictionary(state.Counts);
        }
        else
        {
            foreach (var item in config.Inventory.Denominations)
            {
                if (DenominationKey.TryParse(item.Key, out var key) && key != null)
                {
                    _inventory.SetCount(key, item.Value.InitialCount);
                }
            }
        }

        _history = new TransactionHistory();
        _manager = new CashChangerManager(_inventory, _history);
        
        // Create monitors for each denomination in inventory
        var monitors = _inventory.AllCounts.Select(x => x.Key).Select(key => 
            new CashStatusMonitor(_inventory, key, 
                nearEmptyThreshold: config.Thresholds.NearEmpty, 
                nearFullThreshold: config.Thresholds.NearFull, 
                fullThreshold: config.Thresholds.Full)
        ).ToList();
        
        _statusAggregator = new OverallStatusAggregator(monitors);
        
        _currencyCode = config.CurrencyCode ?? "JPY";

        // Subscribe to overall status changes for StatusUpdateEvent
        _statusSubscription = _statusAggregator.OverallStatus
            .Subscribe(status => 
            {
                _lastCashChangerStatus = status switch
                {
                    CashStatus.Empty => Microsoft.PointOfService.CashChangerStatus.Empty,
                    CashStatus.NearEmpty => Microsoft.PointOfService.CashChangerStatus.NearEmpty,
                    _ => Microsoft.PointOfService.CashChangerStatus.OK
                };
                // StatusUpdateEvent is fired automatically by the base class when DeviceStatus changes.
                // The DeviceStatus property getter already returns the mapped status.
            });
            
        // Listen to inventory changes to track deposits if BeginDeposit was called
        _inventory.Changed.Subscribe(key => 
        {
            if (_depositStatus == CashDepositStatus.None) return;
            
            // Assume any addition to inventory is a deposit during the session.
            // This is a simple simulator logic.
            var count = _inventory.GetCount(key);
            
            // We need to know if it was an 'Add' or 'Set'. 
            // In a better design, Inventory.Changed would include the delta.
            // For now, let's just use a simple heuristic: if it's during a deposit, we'll increment our tracker.
            // (Note: This might be inaccurate if the UI subtracts for correction, but it's a start)
            
            _depositAmount += (int)key.Value; // Increment total amount by face value of 1 item
            _depositCounts[key] = 
                _depositCounts.TryGetValue(key, out int value)
                ? ++value : 1;

            if (DataEventEnabled)
            {
                // Queue a DataEvent for the POS application.
                // DataEventArgs(status=0) indicates successful data.
                QueueEvent(new DataEventArgs(0));
            }
        });
    }

    private readonly string _currencyCode;

    public override string CheckHealth(HealthCheckLevel level) => "OK";
    public override string CheckHealthText => "OK";

    public override void BeginDeposit() 
    {
        _depositAmount = 0;
        _depositCounts.Clear();
        _depositStatus = CashDepositStatus.Count;
    }

    public override void EndDeposit(CashDepositAction action) 
    {
        if (action == CashDepositAction.Change)
        {
            DispenseChange(_depositAmount);
        }
        _depositStatus = CashDepositStatus.None;
    }

    public override void FixDeposit() 
    {
        // For simulation, we assume counting is finished.
        // If DataEventEnabled is true, we should fire DataEvent here or progressively.
        if (DataEventEnabled)
        {
            // Fire DataEvent(amount, 0)
            // In CashChangerBasic, we use specialized methods or fire manually.
            // For now, let's assume we fire it.
        }
    }

    public override void PauseDeposit(CashDepositPause control) 
    {
        if (control == CashDepositPause.Pause) _depositStatus = CashDepositStatus.None; // Using None or something else for paused if no Paused state
        else _depositStatus = CashDepositStatus.Count;
    }
    
    public override CashCounts ReadCashCounts() 
    {
        var list = new List<CashCount>();
        foreach (var kv in _inventory.AllCounts)
        {
            list.Add(new CashCount(kv.Key.Type == CashType.Bill ? CashCountType.Bill : CashCountType.Coin, (int)kv.Key.Value, kv.Value));
        }
        return new CashCounts([.. list],
                              false);
    }

    public override void DispenseChange(int amount) => _manager.Dispense((decimal)amount);

    public override void DispenseCash(CashCount[] cashCounts) 
    {
        foreach (var cc in cashCounts)
        {
            // Need to determine if CC is bill or coin. This is tricky with UPOS CashCount.
            // Usually, the SO knows which is which from the CurrencyCashList.
            // For now, let's try to find a matching Bill first, then Coin.
            // In POS for .NET, CashCount property for amount is 'NominalValue'.
            var billKey = new DenominationKey(cc.NominalValue, CashType.Bill);
            var coinKey = new DenominationKey(cc.NominalValue, CashType.Coin);
            
            if (_inventory.GetCount(billKey) >= cc.Count)
                _inventory.Add(billKey, -cc.Count);
            else if (_inventory.GetCount(coinKey) >= cc.Count)
                _inventory.Add(coinKey, -cc.Count);
        }
    }

    public override DirectIOData DirectIO(int command, int data, object obj) => new(data, obj);

    public override Microsoft.PointOfService.CashChangerStatus DeviceStatus => _lastCashChangerStatus;

    public override CashChangerFullStatus FullStatus 
    {
        get => _statusAggregator.OverallStatus.CurrentValue switch
        {
            CashStatus.Full => CashChangerFullStatus.Full,
            CashStatus.NearFull => CashChangerFullStatus.NearFull,
            _ => CashChangerFullStatus.OK
        };
    }
    
    public override bool AsyncMode { get; set; }
    public override int AsyncResultCode => 0;
    public override int AsyncResultCodeExtended => 0;

    public override string CurrencyCode { get => _currencyCode; set { } }
    public override string[] CurrencyCodeList => [.. new[] { _currencyCode }];
    public override string[] DepositCodeList => [.. new[] { _currencyCode }];
    
    public override CashUnits CurrencyCashList => new();
    public override CashUnits DepositCashList => new();
    public override CashUnits ExitCashList => new();
    
    public override int DepositAmount => _depositAmount;
    public override CashCount[] DepositCounts 
    {
        get => [.. _depositCounts.Select(kv => new CashCount(kv.Key.Type == CashType.Bill ? CashCountType.Bill : CashCountType.Coin, (int)kv.Key.Value, kv.Value))];
    }
    public override CashDepositStatus DepositStatus => _depositStatus;
    
    public override int CurrentExit { get => 1; set { } }
    public override int DeviceExits => 1;

    public override bool CapDeposit => true;
    public override bool CapDepositDataEvent => true;
    public override bool CapPauseDeposit => false;
    public override bool CapDiscrepancy => false;
    public override bool CapRepayDeposit => false;
    public override bool CapFullSensor => true;
    public override bool CapNearFullSensor => true;
    public override bool CapNearEmptySensor => true;
    public override bool CapEmptySensor => true;
}
