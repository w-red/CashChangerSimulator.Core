# OPOS Compliance Mapping

This document describes the relationship between this simulator and the OPOS (OLE for Retail POS) standard, specifically focusing on error mapping.

## ResultCode Mapping

The `PosControlException` thrown by this simulator maps to the following standard UPOS/OPOS error codes:

| ErrorCode          | OPOS Constant (OPOS_E_*) | Trigger Condition                                          |
| :----------------- | :----------------------- | :--------------------------------------------------------- |
| **Success** (0)    | SUCCESS                  | Operation completed successfully.                          |
| **Illegal** (106)  | ILLEGAL                  | Invalid parameter, sequence, or unsupported argument.      |
| **Failure** (111)  | FAILURE                  | Mechanical failure (e.g., Jam) detected during operation.  |
| **Extended** (114) | EXTENDED                 | Device-specific extended error (e.g., Inventory shortage). |
| **Busy** (113)     | BUSY                     | Simultaneous call during an active asynchronous operation. |

## ResultCodeExtended Mapping

Specific extended error definitions for `ErrorCode.Extended`:

| Extended Code | Member Name  | Meaning                                                                 |
| :------------ | :----------: | :---------------------------------------------------------------------- |
| **201**       | OverDispense | Shortage of cash; requested amount or denomination cannot be dispensed. |

> [!NOTE]
> In the source code, these are defined in the `UposCashChangerErrorCodeExtended` enum to avoid magic numbers.

## Event Notification
Events are dispatched through the `QueueEvent` method, appearing as standard `DataEvent` or `StatusUpdateEvent` listeners.
In the Simulator UI, these are logged chronologically in the "Activity Feed" for debugging purposes.

---
*For the Japanese version, see [OposComplianceMapping_JP.md](OposComplianceMapping_JP.md).*
