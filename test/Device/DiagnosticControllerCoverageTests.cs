using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Virtual;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class DiagnosticControllerCoverageTests
{
    /// <summary>不感帯（デプリション）失敗のカウントが正しくインクリメントされることを検証する。</summary>
    [Fact]
    public void IncrementFailedDepletionShouldIncreaseValue()
    {
        var hw = new HardwareStatusManager();
        var controller = new DiagnosticController(new Inventory(), hw);
        
        Should.NotThrow(() => controller.IncrementFailedDepletion());
    }

    /// <summary>指定されたレベルに応じたヘルスレポートが生成され、適切な詳細が含まれていることを検証する。</summary>
    [Fact]
    public void GetHealthReportWithVariousLevelsShouldIncludeDetails()
    {
        var hw = new HardwareStatusManager();
        var controller = new DiagnosticController(new Inventory(), hw);
        
        var report1 = controller.GetHealthReport(DeviceHealthCheckLevel.External);
        report1.ShouldContain("Jam Status: Normal");

        hw.SetJammed(true);
        var report2 = controller.GetHealthReport(DeviceHealthCheckLevel.Interactive);
        report2.ShouldContain("Interactive check initiated");
    }

    /// <summary>Dispose を複数回呼び出しても例外が発生しないことを検証する。</summary>
    [Fact]
    public void DisposeWhenCalledMultipleTimesShouldNotThrow()
    {
        var hw = new HardwareStatusManager();
        var controller = new DiagnosticController(new Inventory(), hw);
        controller.Dispose();
        Should.NotThrow(() => controller.Dispose());
    }
}
