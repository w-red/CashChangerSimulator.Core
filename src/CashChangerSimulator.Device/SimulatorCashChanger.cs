using System;
using System.Linq;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using CashChangerSimulator.Core.Models;

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
        _inventory = new Inventory();
        _history = new TransactionHistory();
        _manager = new CashChangerManager(_inventory, _history);
        
        var denominations = new[] { 10000, 5000, 2000, 1000, 500, 100, 50, 10, 5, 1 };
        var monitors = denominations.Select(d => 
            new CashStatusMonitor(_inventory, d, nearEmptyThreshold: 5, nearFullThreshold: 90, fullThreshold: 100)
        ).ToList();
        
        _statusAggregator = new OverallStatusAggregator(monitors);
    }

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

    public override string CurrencyCode { get => "JPY"; set { } }
    public override string[] CurrencyCodeList => new[] { "JPY" };
    public override string[] DepositCodeList => new[] { "JPY" };
    
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
