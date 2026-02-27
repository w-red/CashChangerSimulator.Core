using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

/// <summary>Test class for providing DirectIOTests functionality.</summary>
public class DirectIOTests
{
    private SimulatorCashChanger CreateSimulator()
    {
        return new SimulatorCashChanger();
    }

    /// <summary>Tests the behavior of DirectIoCommand10ShouldToggleOverlap to ensure proper functionality.</summary>
    [Fact]
    public void DirectIoCommand10ShouldToggleOverlap()
    {
        var simulator = CreateSimulator();
        simulator.SkipStateVerification = true;

        // Force Overlap ON
        var result = simulator.DirectIO(10, 1, null!);
        result.Data.ShouldBe(1);
        
        var hardwareStatusManager = (HardwareStatusManager)typeof(SimulatorCashChanger)
            .GetField("_hardwareStatusManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(simulator)!;

        hardwareStatusManager.IsOverlapped.Value.ShouldBeTrue("DirectIO 10 with data=1 should set Overlap to true.");

        // Force Overlap OFF
        simulator.DirectIO(10, 0, null!);
        hardwareStatusManager.IsOverlapped.Value.ShouldBeFalse("DirectIO 10 with data=0 should set Overlap to false.");
    }

    /// <summary>Tests the behavior of DirectIoCommand11ShouldToggleJam to ensure proper functionality.</summary>
    [Fact]
    public void DirectIoCommand11ShouldToggleJam()
    {
        var simulator = CreateSimulator();
        simulator.SkipStateVerification = true;
        var hardwareStatusManager = (HardwareStatusManager)typeof(SimulatorCashChanger)
            .GetField("_hardwareStatusManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(simulator)!;

        // Force Jam ON
        simulator.DirectIO(11, 1, null!);
        hardwareStatusManager.IsJammed.Value.ShouldBeTrue("DirectIO 11 with data=1 should set Jam to true.");

        // Force Jam OFF
        simulator.DirectIO(11, 0, null!);
        hardwareStatusManager.IsJammed.Value.ShouldBeFalse("DirectIO 11 with data=0 should set Jam to false.");
    }

    /// <summary>Tests the behavior of DirectIoCommand100ShouldReturnVersionInfo to ensure proper functionality.</summary>
    [Fact]
    public void DirectIoCommand100ShouldReturnVersionInfo()
    {
        var simulator = CreateSimulator();
        
        var result = simulator.DirectIO(100, 0, "");
        result.Object.ShouldNotBeNull();
        result.Object.ToString()!.ShouldContain("SimulatorCashChanger");
    }

    /// <summary>Tests the behavior of DirectIoUnknownCommandShouldPassThrough to ensure proper functionality.</summary>
    [Fact]
    public void DirectIoUnknownCommandShouldPassThrough()
    {
        var simulator = CreateSimulator();
        var myObj = new object();
        var result = simulator.DirectIO(999, 123, myObj);
        
        result.Data.ShouldBe(123);
        result.Object.ShouldBe(myObj);
    }
}