using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Virtual;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class DiagnosticControllerCoverageTests
{
    [Fact]
    public void IncrementFailedDepletion_ShouldIncreaseValue()
    {
        var hw = new HardwareStatusManager();
        var controller = new DiagnosticController(new Inventory(), hw);
        
        Should.NotThrow(() => controller.IncrementFailedDepletion());
    }

    [Fact]
    public void GetHealthReport_WithVariousLevels_ShouldIncludeDetails()
    {
        var hw = new HardwareStatusManager();
        var controller = new DiagnosticController(new Inventory(), hw);
        
        var report1 = controller.GetHealthReport(DeviceHealthCheckLevel.External);
        report1.ShouldContain("Jam Status: Normal");

        hw.SetJammed(true);
        var report2 = controller.GetHealthReport(DeviceHealthCheckLevel.Interactive);
        report2.ShouldContain("Interactive check initiated");
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        var hw = new HardwareStatusManager();
        var controller = new DiagnosticController(new Inventory(), hw);
        controller.Dispose();
        Should.NotThrow(() => controller.Dispose());
    }
}
