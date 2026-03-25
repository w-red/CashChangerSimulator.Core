using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>CheckHealth や統計情報（Statistics）などの自己診断機能を検証するテストクラス。</summary>
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

    /// <summary>CheckHealth(Internal) が詳細な内部状態レポートを返却することを検証します。</summary>
    [Fact]
    public void CheckHealthInternal_ShouldReturnDetailedReport()
    {
        var simulator = CreateSimulator();
        
        var report = simulator.CheckHealth(HealthCheckLevel.Internal);
        
        report.ShouldContain("Internal Health Check");
        report.ShouldContain("Inventory: OK");
        report.ShouldContain("Status: OK");
    }

    /// <summary>CheckHealth(External) がハードウェア接続状態を含むレポートを返却することを検証します。</summary>
    [Fact]
    public void CheckHealthExternal_ShouldReturnHardwareReport()
    {
        var simulator = CreateSimulator();
        
        var report = simulator.CheckHealth(HealthCheckLevel.External);
        
        report.ShouldContain("External Health Check");
        report.ShouldContain("Hardware: Connected");
    }

    /// <summary>デバイス統計情報（Statistics）が正しく集計・提供されることを検証します。</summary>
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

    /// <summary>DirectIO(1002) 経由で診断ログが取得できることを検証します。</summary>
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
