using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using Microsoft.PointOfService;
using System.Text;

namespace CashChangerSimulator.Device;

/// <summary>デバイスの診断機能（ヘルスチェック、統計情報）を管理するコントローラー。</summary>
/// <param name="inventory">現金在庫データを管理する <see cref="Inventory"/> モデル。</param>
/// <param name="hardwareStatusManager">デバイスの接続やジャム状態を管理する <see cref="HardwareStatusManager"/>。</param>
/// <remarks>
/// UPOS 標準の <see cref="CashChangerBasic.CheckHealth"/> メソッドに対するレポート生成（<see cref="GetHealthReport"/>）や、
/// 各種操作回数などの統計情報の累積・XML出力（<see cref="RetrieveStatistics"/>）を担当します。
/// </remarks>
public class DiagnosticController(Inventory inventory, HardwareStatusManager hardwareStatusManager)
{
    private static T EnsureNotNull<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }

    private readonly Inventory _inventory = EnsureNotNull(inventory);
    private readonly HardwareStatusManager _hardwareStatusManager = EnsureNotNull(hardwareStatusManager);
    private int _successfulDepletionCount;
    private int _failedDepletionCount;

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
        ArgumentNullException.ThrowIfNull(statistics);

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

    /// <summary>入金失敗回数をカウントアップします。</summary>
    public virtual void IncrementFailedDepletion() => _failedDepletionCount++;
}
