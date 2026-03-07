using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>Tests for detailed diagnostic functions including CheckHealth and Statistics.</summary>
public class DetailedDiagnosticsTests
{
    private static InternalSimulatorCashChanger CreateSimulator()
    {
        var simulator = new InternalSimulatorCashChanger();
        simulator.Open();
        simulator.Claim(1000);
        simulator.DeviceEnabled = true;
        return simulator;
    }

    /// <summary>Verifies that CheckHealth(Internal) returns a detailed report when the system is healthy.</summary>
    [Fact]
    public void CheckHealthInternal_ShouldReturnDetailedReport()
    {
        var simulator = CreateSimulator();
        
        var report = simulator.CheckHealth(HealthCheckLevel.Internal);
        
        report.ShouldContain("Internal Health Check");
        report.ShouldContain("Inventory: OK");
        report.ShouldContain("Status: OK");
    }

    /// <summary>Verifies that CheckHealth(External) returns a report about simulated hardware connection.</summary>
    [Fact]
    public void CheckHealthExternal_ShouldReturnHardwareReport()
    {
        var simulator = CreateSimulator();
        
        var report = simulator.CheckHealth(HealthCheckLevel.External);
        
        report.ShouldContain("External Health Check");
        report.ShouldContain("Hardware: Connected");
    }

    /// <summary>Verifies that statistics are tracked and can be retrieved.</summary>
    [Fact]
    public void RetrieveStatistics_ShouldReturnOperationCounts()
    {
        var simulator = CreateSimulator();
        
        // Perform some operations
        simulator.BeginDeposit();
        simulator.FixDeposit();
        simulator.EndDeposit(CashDepositAction.NoChange);
        
        string[] stats = ["*"];
        var result = simulator.RetrieveStatistics(stats);
        
        result.ShouldContain("SuccessfulDepletionCount");
    }

    /// <summary>Verifies that Diagnostic Log can be retrieved via DirectIO (1002).</summary>
    [Fact]
    public void DirectIO_GetDiagnosticLog_ShouldReturnReport()
    {
        var simulator = CreateSimulator();
        simulator.CheckHealth(HealthCheckLevel.Internal);
        
        var result = simulator.DirectIO(1002, 0, "");
        
        result.Object.ShouldNotBeNull();
        result.Object.ToString()!.ShouldContain("Internal Health Check");
    }
}
