# UPOS Compliance Mapping (CashChanger)

This document outlines the implementation status of the CashChanger simulator relative to the UnifiedPOS (UPOS) v1.15.1 specification.

## Legend
- ✅: Implemented
- ⚠️: Partially Implemented / Simulation Only
- ❌: Not Implemented
- N/A: Not applicable for this simulator

## Methods

| Method                  | Status | Notes                                                                          |
| :---------------------- | :----: | :----------------------------------------------------------------------------- |
| **Open** / **Close**    |   ✅    | Emulates basic device lifecycle.                                               |
| **Claim** / **Release** |   ✅    | Emulates exclusive access control.                                             |
| **CheckHealth**         |   ✅    | Provides basic device self-test simulation.                                    |
| **DirectIO**            |   ✅    | Supports custom commands (e.g., SET_OVERLAP, SET_JAM, ADJUST_CASH_COUNTS_STR). |
| **BeginDeposit**        |   ✅    | Starts a new deposit session.                                                  |
| **EndDeposit**          |   ✅    | Finalizes deposit and dispenses change based on `CashDepositAction`.           |
| **FixDeposit**          |   ✅    | Commits the inserted amount to the virtual inventory.                          |
| **PauseDeposit**        |   ✅    | Suspends and resumes the deposit session.                                      |
| **ReadCashCounts**      |   ✅    | Retrieves the full list of current inventory counts.                           |
| **DispenseChange**      |   ✅    | Dispenses a total amount using the internal change calculation algorithm.      |
| **DispenseCash**        |   ✅    | Dispenses specific denominations and counts.                                   |
| **AdjustCashCounts**    |   ✅    | Logically increases or decreases the specific denomination counts.             |

## Properties

| Property                | Status | Notes                                                                 |
| :---------------------- | :----: | :-------------------------------------------------------------------- |
| **DeviceEnabled**       |   ✅    |                                                                       |
| **DataEventEnabled**    |   ✅    |                                                                       |
| **AsyncMode**           |   ✅    | Supports asynchronous execution for dispensing operations.            |
| **CurrencyCode**        |   ✅    | Sets/Gets the active currency for operations.                         |
| **CurrencyCodeList**    |   ✅    | Dynamically generated from the TOML configuration.                    |
| **DepositAmount**       |   ✅    | Tracks the total cash inserted in the current session.                |
| **DepositCashList**     |   ✅    | Lists the counts of specific denominations inserted.                  |
| **DeviceStatus**        |   ✅    | Reports OK, EMPTY, NEAR_EMPTY, etc.                                   |
| **FullStatus**          |   ✅    | Reports OK, FULL, NEAR_FULL, etc.                                     |
| **RealTimeDataEnabled** |   ✅    | When enabled, fires `DataEvent` immediately upon each cash insertion. |

## Events

| Event                   | Status | Notes                                                               |
| :---------------------- | :----: | :------------------------------------------------------------------ |
| **DataEvent**           |   ✅    | Fired upon deposit completion or real-time notification.            |
| **StatusUpdateEvent**   |   ✅    | Notifies state transitions (e.g., OK, Jam, Removed, AsyncFinished). |
| **ErrorEvent**          |   ✅    | Notifications for failed asynchronous operations.                   |
| **OutputCompleteEvent** |   ✅    | Notifications for successful asynchronous completion.               |

---
*For the Japanese version, see [UposComplianceMapping_JP.md](UposComplianceMapping_JP.md).*
