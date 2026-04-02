using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.Device;

/// <summary>現金入出金機の基本操作と状態監視を定義する抽象インターフェース。</summary>
public interface ICashChangerDevice : IDisposable
{
    // Lifecycle
    Task OpenAsync();
    Task CloseAsync();

    // Operations
    Task BeginDepositAsync();
    Task FixDepositAsync();
    Task EndDepositAsync(DepositAction action);
    Task DispenseAsync(int amount);
    Task DispenseAsync(IEnumerable<CashDenominationCount> counts);
    
    // Inventory
    Task<Inventory> ReadInventoryAsync();
    Task AdjustInventoryAsync(IEnumerable<CashDenominationCount> counts);

    // Diagnostics
    Task<string> CheckHealthAsync();

    // Observable States
    ReadOnlyReactiveProperty<bool> IsBusy { get; }
    ReadOnlyReactiveProperty<DeviceControlState> State { get; }
}

