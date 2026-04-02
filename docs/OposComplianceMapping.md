# OPOS Compliance Mapping

This document describes the relationship between this simulator and the OPOS (OLE for Retail POS) standard, specifically focusing on error mapping.

## ResultCode Mapping

The `PosControlException` thrown by this simulator maps to the following standard UPOS/OPOS error codes:

| ErrorCode             | OPOS Constant (OPOS_E_*) | Trigger Condition                                          |
| :-------------------- | :----------------------- | :--------------------------------------------------------- |
| **Success** (0)       | SUCCESS                  | Operation completed successfully.                          |
| **Closed** (101)      | CLOSED                   | Device is closed (not opened).                             |
| **NotClaimed** (102)  | NOTCLAIMED               | Device is claimed by another process.                      |
| **NotOpen** (103)     | NOTOPEN                  | Device has not been opened.                                |
| **Disabled** (104)    | DISABLED                 | Device is not enabled (DeviceEnabled=False).               |
| **Illegal** (106)     | ILLEGAL                  | Invalid parameter, sequence, or unsupported argument.      |
| **NoHardware** (107)  | NOHARDWARE               | Physical hardware not found or disconnected.               |
| **Offline** (108)     | OFFLINE                  | Device is offline.                                         |
| **NoService** (109)   | NOSERVICE                | Control service is not available.                          |
| **Failure** (111)     | FAILURE                  | Mechanical failure (e.g., Jam) detected during operation.  |
| **Timeout** (112)     | TIMEOUT                  | Response timeout from the device.                          |
| **Busy** (113)        | BUSY                     | Simultaneous call during an active asynchronous operation. |
| **Extended** (114)    | EXTENDED                 | Device-specific extended error (e.g., Inventory shortage). |
| **NoInventory** (118) | (Custom)                 | Inventory shortage (Simulator-specific custom code).       |
| **Unimplemented** (119)| (Custom)                | Call to an unimplemented feature.                          |
| **Jammed** (300)      | (Custom)                 | Hardware jam occurred.                                     |
| **Overlapped** (301)  | (Custom)                 | Overlapped operation detected.                             |

## ResultCodeExtended Mapping

Specific extended error definitions for `ErrorCode.Extended`:

| Extended Code | Member Name  | Meaning                                                                 |
| :------------ | :----------: | :---------------------------------------------------------------------- |
| **201**       | OverDispense | Shortage of cash; requested amount or denomination cannot be dispensed. |

> [!NOTE]
> In the source code, these are defined in the `UposCashChangerErrorCodeExtended` enum to avoid magic numbers.

## Event Notification
Events are dispatched through the `QueueEvent` method, appearing as standard2. **Testability**: `Device.Virtual` can be tested in isolation, allowing verification of simulation logic without expensive hardware or Windows SDK environments.
3. **Extensibility**: Adding new communication methods (e.g., gRPC, Web Serial) only requires adding a new adapter project.

## Reliability & Synchronization Hardening

The project prioritizes reliability, especially during asynchronous operations like cash dispensing.

- **Deterministic State Transitions**: `DispenseController` ensures that internal state updates occur precisely before invoking completion callbacks, allowing `UposMediator` to finalize all properties (e.g., `AsyncResultCode`) before firing events.
- **Race Condition Elimination**: Synchronization mechanisms have been hardened to ensure that event notifications and property reads occur atomically, maintaining stability even under high-load testing environments.
- **UPOS-Compliant Error Mapping**: `DeviceErrorCode` strictly adheres to UPOS/OPOS standard integer values, maximizing compatibility as an external Service Object (SO).

---
*For the Japanese version, see [OposComplianceMapping_JP.md](OposComplianceMapping_JP.md).*
