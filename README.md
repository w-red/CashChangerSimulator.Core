# CashChanger Simulator - Core

[![NuGet Version](https://img.shields.io/nuget/v/CashChangerSimulator.Core)](https://www.nuget.org/packages/CashChangerSimulator.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

This repository contains the core logic and hardware device emulation for the CashChanger Simulator. It is designed as a modular foundational component that powers various user interfaces.

## 📦 NuGet Packages in this Repository

The following packages are maintained in this repository:

- **CashChangerSimulator.Core**: Platform-independent core logic, currency calculation, and managers.
- **CashChangerSimulator.Device**: Abstract device interfaces and common simulation infrastructure.
- **CashChangerSimulator.Device.Virtual**: Pure C# virtual hardware simulation (works on Web/Linux/Windows, .NET 10).
- **CashChangerSimulator.Device.PosForDotNet**: Windows-specific UPOS (POS for .NET) adapter for legacy integration.

> [!NOTE]
> User interface components are maintained in their own dedicated repositories:
> [**Cli** (Command Line Interface)](https://github.com/w-red/CashChangerSimulator.Cli) and [**Wpf** (Windows Desktop UI)](https://github.com/w-red/CashChangerSimulator.Wpf).

---

## Key Features (Platform-Independent Core)

- **Decoupled Architecture**: Strictly separates business logic (`Core`), simulation logic (`Device.Virtual`), and hardware interface adapters (`Device.PosForDotNet`).
- **UPOS Compliant Behavior**: Emulates `DispenseChange`, `DispenseCash`, and the full deposit cycle through a virtual device layer.
- **Multi-Platform Ready**: The Core and Virtual Device projects have zero dependency on Windows-specific libraries, enabling use in Web APIs, CLIs, or Linux environments.
- **Inventory & Transaction Logic**: Robust state management for cash inventory and deposit tracking using platform-independent types.

## Setup

### Using NuGet (Recommended)

You can install the official packages via NuGet.org or GitHub Packages:

```powershell
dotnet add package CashChangerSimulator.Core
dotnet add package CashChangerSimulator.Device
```

### Local Build

If you need to build from source:

```powershell
# Build the core library and device simulator
dotnet build
```

---

## 🚀 Live Demo / Simulator API

The Virtual Cash Changer API is available on Google Cloud Run for testing without local setup:

- **Interactive Documentation (Scalar)**: [**View API Reference**](https://cash-changer-api-904915502524.asia-northeast1.run.app/scalar/v1)

---

## Documentation

For more detailed information, please refer to the documents in the `docs/` directory:

- [Architecture Overview](docs/Architecture.md): High-level system design.
- [UPOS Compliance Mapping](docs/UposComplianceMapping.md): Status of UPOS interface implementation.
- [OPOS Compliance Mapping](docs/OposComplianceMapping.md): Mapping of OPOS error codes and result codes.

---
*For the Japanese version, see [README_JP.md](README_JP.md).*
