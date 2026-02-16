using System;
using System.Linq;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.Device;

[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Virtual Cash Changer Simulator", 1, 14)]
public class SimulatorCashChanger : CashChangerBasic
{
    private readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly CashChangerManager _manager;
    private readonly OverallStatusAggregator _statusAggregator;

    public SimulatorCashChanger()
    {
        // Load settings from TOML
        var config = ConfigurationLoader.Load();

        _inventory = new Inventory();
        foreach (var item in config.Inventory.InitialCounts)
        {
            if (DenominationKey.TryParse(item.Key, out var key) && key != null)
            {
                _inventory.SetCount(key, item.Value);
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
    }

    private readonly string _currencyCode;

    public override string CheckHealth(HealthCheckLevel level) => "OK";
    public override string CheckHealthText => "OK";

    public override void BeginDeposit() { }
    public override void EndDeposit(CashDepositAction action) { }
    public override void FixDeposit() { }
    public override void PauseDeposit(CashDepositPause control) { }
    
    public override CashCounts ReadCashCounts() => new CashCounts();
    public override void DispenseChange(int amount) => _manager.Dispense((decimal)amount);
    public override void DispenseCash(CashCount[] cashCounts) { }

    public override DirectIOData DirectIO(int command, int data, object obj) => new DirectIOData(data, obj);

    public override Microsoft.PointOfService.CashChangerStatus DeviceStatus => Microsoft.PointOfService.CashChangerStatus.OK;
    public override CashChangerFullStatus FullStatus => CashChangerFullStatus.OK;
    
    public override bool AsyncMode { get; set; }
    public override int AsyncResultCode => 0;
    public override int AsyncResultCodeExtended => 0;

    public override string CurrencyCode { get => _currencyCode; set { } }
    public override string[] CurrencyCodeList => new[] { _currencyCode };
    public override string[] DepositCodeList => new[] { _currencyCode };
    
    public override CashUnits CurrencyCashList => new CashUnits();
    public override CashUnits DepositCashList => new CashUnits();
    public override CashUnits ExitCashList => new CashUnits();
    
    public override int DepositAmount => 0;
    public override CashCount[] DepositCounts => Array.Empty<CashCount>();
    public override CashDepositStatus DepositStatus => CashDepositStatus.None;
    
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
