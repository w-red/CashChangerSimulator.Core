using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using System.Text;
using System.Linq;

namespace CashChangerSimulator.Device;

/// <summary>デバイスの診断機能（健康診断、統計情報）を管理するコントローラー。</summary>
public class DiagnosticController
{
    private readonly Inventory _inventory;
    private readonly CashChangerSimulator.Core.Managers.HardwareStatusManager _hardwareStatusManager;
    private readonly ILogger<DiagnosticController> _logger;

    // 統計情報（UPOS標準）
    private long _successfulDepletionCount;
    private long _failedDepletionCount;
    private long _serviceCount;

    public DiagnosticController(Inventory inventory, CashChangerSimulator.Core.Managers.HardwareStatusManager hardwareStatusManager, ILogger<DiagnosticController> logger)
    {
        _inventory = inventory;
        _hardwareStatusManager = hardwareStatusManager;
        _logger = logger;
    }

    /// <summary>健康状態の報告書を作成します。</summary>
    public virtual string GetHealthReport(HealthCheckLevel level)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- {level} Health Check Report ---");

        switch (level)
        {
            case HealthCheckLevel.Internal:
                sb.AppendLine("Inventory: OK");
                sb.AppendLine($"Total Denominations: {_inventory.AllCounts.Count()}");
                sb.AppendLine("Status: OK");
                break;
            case HealthCheckLevel.External:
                sb.AppendLine($"Hardware: {(_hardwareStatusManager.IsConnected.Value ? "Connected" : "Disconnected")}");
                sb.AppendLine($"Jam Status: {(_hardwareStatusManager.IsJammed.Value ? "Jammed" : "Normal")}");
                break;
            case HealthCheckLevel.Interactive:
                sb.AppendLine("Interactive check initiated. Please verify LED patterns.");
                break;
        }

        return sb.ToString();
    }

    /// <summary>統計情報を取得します。</summary>
    public virtual string RetrieveStatistics(string[] statistics)
    {
        // シンプルな XML 形式での返却（UPOS標準に準拠）
        var sb = new StringBuilder();
        sb.AppendLine("<CommonStatistics>");
        if (statistics.Contains("*") || statistics.Contains("SuccessfulDepletionCount"))
        {
            sb.AppendLine($"  <SuccessfulDepletionCount>{_successfulDepletionCount}</SuccessfulDepletionCount>");
        }
        if (statistics.Contains("*") || statistics.Contains("FailedDepletionCount"))
        {
            sb.AppendLine($"  <FailedDepletionCount>{_failedDepletionCount}</FailedDepletionCount>");
        }
        sb.AppendLine("</CommonStatistics>");
        return sb.ToString();
    }

    /// <summary>入金成功回数をカウントアップします。</summary>
    public virtual void IncrementSuccessfulDepletion() => _successfulDepletionCount++;
}
