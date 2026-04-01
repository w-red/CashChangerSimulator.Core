using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using System.Text;
using CashChangerSimulator.Device;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>デバイスの診断機能（ヘルスチェック、統計情報）を管理するコントローラー（仮想デバイス実装）。</summary>
/// <param name="inventory">現金在庫データを管理する <see cref="Inventory"/> モデル。</param>
/// <param name="hardwareStatusManager">デバイスの接続やジャム状態を管理する <see cref="HardwareStatusManager"/>。</param>
/// <remarks>
/// UPOS などのプラットフォーム固有の SDK に依存せず、純粋な C# ロジックとして診断機能を提供します。
/// </remarks>
public class DiagnosticController(Inventory inventory, HardwareStatusManager hardwareStatusManager) : IDisposable
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
    public virtual string GetHealthReport(DeviceHealthCheckLevel level)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- {level} Health Check Report ---");

        switch (level)
        {
            case DeviceHealthCheckLevel.Internal:
                sb.AppendLine("Inventory: OK");
                sb.AppendLine($"Total Denominations: {_inventory.AllCounts.Count()}");
                sb.AppendLine("Status: OK");
                break;
            case DeviceHealthCheckLevel.External:
                sb.AppendLine($"Hardware: {(_hardwareStatusManager.IsConnected.Value ? "Connected" : "Disconnected")}");
                sb.AppendLine($"Jam Status: {(_hardwareStatusManager.IsJammed.Value ? "Jammed" : "Normal")}");
                break;
            case DeviceHealthCheckLevel.Interactive:
                sb.AppendLine("Interactive check initiated. Please verify LED patterns.");
                break;
        }

        return sb.ToString();
    }

    /// <summary>統計情報を取得します。</summary>
    public virtual string RetrieveStatistics(string[] statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);

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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
