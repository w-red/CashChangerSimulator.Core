using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual;
using PosSharp.Abstractions;
using Shouldly;

namespace CashChangerSimulator.Tests.Device.Virtual;

public class DiagnosticControllerMutationTests
{
    private readonly DiagnosticController _controller;
    private readonly HardwareStatusManager _statusManager;
    private readonly Inventory _inventory;

    public DiagnosticControllerMutationTests()
    {
        _statusManager = HardwareStatusManager.Create();
        _inventory = Inventory.Create();
        // DiagnosticController only takes Inventory and HardwareStatusManager
        _controller = new DiagnosticController(_inventory, _statusManager);
    }

    [Fact]
    public void GetHealthReport_Internal_ReturnsInventoryInfo()
    {
        var report = _controller.GetHealthReport(HealthCheckLevel.Internal);
        report.ShouldContain("Inventory: OK");
    }

    [Fact]
    public void RetrieveStatistics_WithAsterisk_ReturnsAllStats()
    {
        _controller.IncrementSuccessfulDepletion();
        var stats = _controller.RetrieveStatistics(new[] { "*" });
        stats.ShouldContain("<SuccessfulDepletionCount>1</SuccessfulDepletionCount>");
    }
}
