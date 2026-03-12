# CashChanger Simulator

A WPF-based cash changer simulator that emulates UnifiedPOS (UPOS) standard operations. Designed to support testing and debugging of POS applications.

## 🚀 Live Demo / Simulator URL

The Virtual Cash Changer API is now available on Google Cloud Run for testing without local setup:

- **Base URL**: [https://cash-changer-api-904915502524.asia-northeast1.run.app](https://cash-changer-api-904915502524.asia-northeast1.run.app)
- **Interactive Documentation (Scalar)**: [View API Reference](https://cash-changer-api-904915502524.asia-northeast1.run.app/scalar/v1)

---

## Key Features

- **UPOS Compliant Behavior**: Emulates `DispenseChange`, `DispenseCash`, and the full deposit cycle (`BeginDeposit` to `EndDeposit`).
- **Multi-Currency Support**: Configurable denominations for various currencies (e.g., JPY, USD).
- **Real-Time Feed**: Provides immediate visual feedback for all cash events, status changes, and errors.
- **Discrepancy Simulation**: Explicitly simulate inventory discrepancy states for robust exception handling testing.
- **Scripted Automation**: Execute complex scenarios via JSON-based automation scripts.

## Setup

### Prerequisites

- .NET 10.0 SDK
- Windows OS (Required for WPF)

### Build and Run

1. Clone or download the repository.
2. Open a terminal in the root directory and run:

```powershell
# Build the project
dotnet build

# Run the simulator UI
dotnet run --project src/CashChangerSimulator.UI.Wpf/CashChangerSimulator.UI.Wpf.csproj
```

### Running Tests

```powershell
# Run all unit, integration, and UI tests
dotnet test
```

## Documentation

For more detailed information, please refer to the documents in the `docs/` directory:

- [Architecture Overview](docs/Architecture.md): High-level system design.
- [UPOS Compliance Mapping](docs/UposComplianceMapping.md): Status of UPOS interface implementation.
- [OPOS Compliance Mapping](docs/OposComplianceMapping.md): Mapping of OPOS error codes and result codes.
- [Operating Instructions (GUI)](docs/ApplicationOperatingInstructions.md): Guide for manual GUI operations.
- [Operating Instructions (CLI)](docs/CliOperatingInstructions.md)
- [POS Mode Operation Guide](docs/PosModeApplicationOperatingInstructions.md): Guide for POS integration and error scenario testing.

---
*For the Japanese version, see [README_JP.md](README_JP.md).*
