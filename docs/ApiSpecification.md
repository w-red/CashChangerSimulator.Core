# API Specification - CashChanger Simulator

The CashChanger Simulator provides two primary ways to interact with the simulated hardware: via the **POS for .NET (UPOS) standard interface** or via a **native C# reactive interface**.

## 1. Initialization and Configuration

The simulator is initialized using the `SimulatorDependencies` record, which allows you to inject custom providers or use default implementations.

### SimulatorDependencies (Record)
| Property | Type | Description |
| :--- | :--- | :--- |
| `ConfigProvider` | `ConfigurationProvider?` | Provides configuration logic (e.g., loading from TOML). |
| `Inventory` | `Inventory?` | Initial stock of cash in the device. |
| `Manager` | `CashChangerManager?` | Core coordination logic for cash operations. |
| `DepositController` | `DepositController?` | Logic for the deposit cycle. |
| `DispenseController` | `DispenseController?` | Logic for cash dispensing. |
| `Mediator` | `IUposMediator?` | Orchestrates UPOS state and events. |

---

## 2. POS for .NET Interface (`SimulatorCashChanger`)

The `SimulatorCashChanger` class implements the `Microsoft.PointOfService.CashChanger` standard.

### Core Lifecycle
- `Open()`: Opens the device.
- `Close()`: Closes the device.
- `Claim(int timeout)`: Effectively "locks" the device for exclusive use.
- `Release()`: Releases the exclusive lock.
- `DeviceEnabled = true/false`: Activates or deactivates the device.

### Deposit Operations
- `BeginDeposit()`: Starts the deposit receiving cycle.
- `EndDeposit(CashDepositAction action)`: Ends the cycle and either completes (commits) or returns (repay) the cash.
- `FixDeposit()`: Fixes currently deposited notes so they can't be repaid during the cycle.
- `PauseDeposit(CashDepositPause control)`: Pauses or resumes the deposit process.

### Dispense Operations
- `DispenseChange(int amount)`: Dispenses the specified amount using the optimal combination of units.
- `DispenseCash(CashCount[] counts)`: Dispenses specific quantities of specific denominations.

### Inventory and Status
- `ReadCashCounts()`: Returns the current inventory counts.
- `AdjustCashCounts(CashCount[] counts)`: Manually adds or removes cash from the inventory.
- `PurgeCash()`: Clears all cash from the bins to the collection box.

### Extended Async Methods
The simulator provides `Task`-based asynchronous wrappers for modern .NET applications:
- `OpenAsync()`, `CloseAsync()`
- `ClaimAsync(timeout)`, `ReleaseAsync()`
- `BeginDepositAsync()`, `EndDepositAsync(action)`, `FixDepositAsync()`
- `DispenseChangeAsync(amount)`, `DispenseCashAsync(counts)`

---

## 3. Native C# Reactive Interface (`ICashChangerDevice`)

For cross-platform applications (Web, Linux) that don't use POS for .NET, use the `ICashChangerDevice` interface directly (provided by `VirtualCashChangerDevice`).

### Reactive Properties (using R3)
- `IsBusy`: `ReadOnlyReactiveProperty<bool>` - Indicates if an operation is in progress.
- `State`: `ReadOnlyReactiveProperty<DeviceControlState>` - Current state (Idle, Busy, Error, etc.).

### Observables
- `DataEvents`: Emits when cash is inserted or processed.
- `ErrorEvents`: Emits when an error occurs in the hardware layer.
- `StatusUpdateEvents`: Emits when device health status changes (e.g., Near Full, Empty).

---

## 4. Common Data Structures

### CashCount
Used in `DispenseCash` and `AdjustCashCounts`.
- `Denomination`: Value of the currency unit.
- `Count`: Number of units.

### DeviceControlState (Enum)
- `Closed`, `Idle`, `Busy`, `Error`.
