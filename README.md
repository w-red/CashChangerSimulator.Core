# CashChanger Simulator - Core

This repository contains the core logic and hardware device emulation for the CashChanger Simulator. It is designed as a modular foundational component that powers various user interfaces.

## 📦 Repositories in this Project

This project is split into several modular repositories:

- **[CashChangerSimulator.Core](https://github.com/w-red/CashChangerSimulator.Core)**: Platform-independent core logic, managers, and shared models.
- **[CashChangerSimulator.Device.Virtual](https://github.com/w-red/CashChangerSimulator.Core/tree/main/src/Device.Virtual)**: Platform-independent device simulation logic and controllers.
- **[CashChangerSimulator.Device.PosForDotNet](https://github.com/w-red/CashChangerSimulator.Core/tree/main/src/Device.PosForDotNet)**: Windows-specific POS for .NET (UPOS) adapter layer.

---

## 🚀 Live Demo / Simulator API

The Virtual Cash Changer API is available on Google Cloud Run for testing without local setup:

- **Base URL**: [https://cash-changer-api-904915502524.asia-northeast1.run.app](https://cash-changer-api-904915502524.asia-northeast1.run.app)
- **Interactive Documentation (Scalar)**: [View API Reference](https://cash-changer-api-904915502524.asia-northeast1.run.app/scalar/v1)

---

## Key Features (Platform-Independent Core)

- **Decoupled Architecture**: Strictly separates business logic (`Core`), simulation logic (`Device.Virtual`), and hardware interface adapters (`Device.PosForDotNet`).
- **UPOS Compliant Behavior**: Emulates `DispenseChange`, `DispenseCash`, and the full deposit cycle through a virtual device layer.
- **Multi-Platform Ready**: The Core and Virtual Device projects have zero dependency on Windows-specific libraries, enabling use in Web APIs, CLIs, or Linux environments.
- **Inventory & Transaction Logic**: Robust state management for cash inventory and deposit tracking using platform-independent types.

## Setup

### Prerequisites

- .NET 10.0 SDK

### Build

```powershell
# Build the core library and device simulator
dotnet build
```

### Publishing for Local Use (NuGet)

To use these libraries in your UI projects, pack and publish them to a local NuGet source:

```powershell
./scripts/publish_local.ps1
```

## Documentation

For more detailed information, please refer to the documents in the `docs/` directory:

- [Architecture Overview](docs/Architecture.md): High-level system design.
- [UPOS Compliance Mapping](docs/UposComplianceMapping.md): Status of UPOS interface implementation.
- [OPOS Compliance Mapping](docs/OposComplianceMapping.md): Mapping of OPOS error codes and result codes.

---
*For the Japanese version, see [README_JP.md](README_JP.md).*
