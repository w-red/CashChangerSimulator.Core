using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual;
using PosSharp.Abstractions;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device.Virtual;

public class DiagnosticControllerTests : IDisposable
{
    private readonly Inventory _inventory;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly DiagnosticController _target;

    public DiagnosticControllerTests()
    {
        _inventory = Inventory.Create();
        _hardwareStatusManager = HardwareStatusManager.Create();
        _target = new DiagnosticController(_inventory, _hardwareStatusManager);
    }

    public void Dispose()
    {
        _target.Dispose();
        _hardwareStatusManager.Dispose();
    }

    [Fact]
    public void GetHealthReport_Internal_ReturnsValidReport()
    {
        // Arrange
        _inventory.Add(new DenominationKey(1000, CurrencyCashType.Bill), 10);

        // Act
        var report = _target.GetHealthReport(HealthCheckLevel.Internal);

        // Assert
        report.ShouldContain("--- Internal Health Check Report ---");
        report.ShouldContain("Inventory: OK");
        report.ShouldContain("Total Denominations: 1");
        report.ShouldContain("Status: OK");
    }

    [Fact]
    public void GetHealthReport_External_ReturnsValidReport()
    {
        // Arrange
        _hardwareStatusManager.Input.IsConnected.Value = true;

        // Act
        var report = _target.GetHealthReport(HealthCheckLevel.External);

        // Assert
        report.ShouldContain("--- External Health Check Report ---");
        report.ShouldContain("Hardware: Connected"); // Default
        report.ShouldContain("Jam Status: Normal"); // Default
    }

    [Fact]
    public void GetHealthReport_Interactive_ReturnsValidReport()
    {
        // Act
        var report = _target.GetHealthReport(HealthCheckLevel.Interactive);

        // Assert
        report.ShouldContain("--- Interactive Health Check Report ---");
        report.ShouldContain("Interactive check initiated");
    }

    [Fact]
    public void RetrieveStatistics_WithAllFilter_ReturnsAllStats()
    {
        // Arrange
        _target.IncrementSuccessfulDepletion();
        _target.IncrementFailedDepletion();
        _target.IncrementFailedDepletion();

        // Act
        var stats = _target.RetrieveStatistics(new[] { "*" });

        // Assert
        stats.ShouldContain("<SuccessfulDepletionCount>1</SuccessfulDepletionCount>");
        stats.ShouldContain("<FailedDepletionCount>2</FailedDepletionCount>");
    }

    [Fact]
    public void RetrieveStatistics_WithSpecificFilter_ReturnsOnlyRequestedStats()
    {
        // Arrange
        _target.IncrementSuccessfulDepletion();

        // Act
        var stats = _target.RetrieveStatistics(new[] { "SuccessfulDepletionCount" });

        // Assert
        stats.ShouldContain("<SuccessfulDepletionCount>1</SuccessfulDepletionCount>");
        stats.ShouldNotContain("<FailedDepletionCount>");
    }

    [Fact]
    public void RetrieveStatistics_WhenFilterIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentNullException>(() => _target.RetrieveStatistics(null!));
        ex.ParamName.ShouldBe("statistics");
    }

    [Fact]
    public void Methods_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _target.Dispose();

        // Act & Assert
        Should.Throw<ObjectDisposedException>(() => _target.GetHealthReport(HealthCheckLevel.Internal));
        Should.Throw<ObjectDisposedException>(() => _target.RetrieveStatistics(new[] { "*" }));
    }

    [Fact]
    public void Constructor_WhenInventoryIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DiagnosticController(null!, _hardwareStatusManager));
    }

    [Fact]
    public void Constructor_WhenHardwareStatusManagerIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DiagnosticController(_inventory, null!));
    }

    [Fact]
    public void GetHealthReport_External_WhenDisconnected_ShowsDisconnected()
    {
        // Arrange
        _hardwareStatusManager.Input.IsConnected.Value = false;

        // Act
        var report = _target.GetHealthReport(HealthCheckLevel.External);

        // Assert
        report.ShouldContain("Hardware: Disconnected");
    }

    [Fact]
    public void GetHealthReport_External_WhenJammed_ShowsJammed()
    {
        // Arrange
        _hardwareStatusManager.Input.IsJammed.Value = true;

        // Act
        var report = _target.GetHealthReport(HealthCheckLevel.External);

        // Assert
        report.ShouldContain("Jam Status: Jammed");
    }

    [Fact]
    public void RetrieveStatistics_OutputContainsXmlTags()
    {
        // Act
        var stats = _target.RetrieveStatistics(new[] { "*" });

        // Assert
        stats.ShouldContain("<CommonStatistics>");
        stats.ShouldContain("</CommonStatistics>");
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        // Act & Assert
        _target.Dispose();
        _target.Dispose(); // Should not throw
    }
}
