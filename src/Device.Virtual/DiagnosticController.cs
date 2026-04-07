using System.Globalization;
using System.Text;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>デバイスの診断機能（ヘルスチェック、統計情報）を管理するコントローラー（仮想デバイス実装）。</summary>
/// <remarks>
/// UPOS などのプラットフォーム固有の SDK に依存せず、純粋な C# ロジックとして診断機能を提供します。
/// </remarks>
public class DiagnosticController : IDisposable
{
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hardwareStatusManager;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticController"/> class.
    /// </summary>
    /// <param name="inventory">現金在庫データを管理する <see cref="Inventory"/> モデル。</param>
    /// <param name="hardwareStatusManager">デバイスの接続やジャム状態を管理する <see cref="HardwareStatusManager"/>。</param>
    public DiagnosticController(Inventory inventory, HardwareStatusManager hardwareStatusManager)
    {
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.hardwareStatusManager = hardwareStatusManager ?? throw new ArgumentNullException(nameof(hardwareStatusManager));
    }

    /// <summary>入金成功回数を取得します。</summary>
    public int SuccessfulDepletionCount { get; private set; }

    /// <summary>入金失敗回数を取得します。</summary>
    public int FailedDepletionCount { get; private set; }

    /// <summary>健康状態の報告書を作成します。</summary>
    /// <param name="level">ヘルスチェックのレベル。</param>
    /// <returns>ヘルスチェック報告書の文字列。</returns>
    public virtual string GetHealthReport(DeviceHealthCheckLevel level)
    {
        var sb = new StringBuilder();
        sb.AppendFormat(CultureInfo.InvariantCulture, "--- {0} Health Check Report ---{1}", level, Environment.NewLine);

        switch (level)
        {
            case DeviceHealthCheckLevel.Internal:
                sb.AppendLine("Inventory: OK");
                sb.AppendFormat(CultureInfo.InvariantCulture, "Total Denominations: {0}{1}", inventory.AllCounts.Count(), Environment.NewLine);
                sb.AppendLine("Status: OK");
                break;
            case DeviceHealthCheckLevel.External:
                sb.AppendFormat(CultureInfo.InvariantCulture, "Hardware: {0}{1}", hardwareStatusManager.IsConnected.Value ? "Connected" : "Disconnected", Environment.NewLine);
                sb.AppendFormat(CultureInfo.InvariantCulture, "Jam Status: {0}{1}", hardwareStatusManager.IsJammed.Value ? "Jammed" : "Normal", Environment.NewLine);
                break;
            case DeviceHealthCheckLevel.Interactive:
                sb.AppendLine("Interactive check initiated. Please verify LED patterns.");
                break;
        }

        return sb.ToString();
    }

    /// <summary>統計情報を取得します。</summary>
    /// <param name="statistics">取得する統計情報の名前。</param>
    /// <returns>統計情報の XML 表現。</returns>
    public virtual string RetrieveStatistics(string[] statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);

        var sb = new StringBuilder();
        sb.AppendLine("<CommonStatistics>");
        if (statistics.Contains("*") || statistics.Contains("SuccessfulDepletionCount"))
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "  <SuccessfulDepletionCount>{0}</SuccessfulDepletionCount>{1}", SuccessfulDepletionCount, Environment.NewLine);
        }

        if (statistics.Contains("*") || statistics.Contains("FailedDepletionCount"))
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "  <FailedDepletionCount>{0}</FailedDepletionCount>{1}", FailedDepletionCount, Environment.NewLine);
        }

        sb.AppendLine("</CommonStatistics>");
        return sb.ToString();
    }

    /// <summary>入金成功回数をカウントアップします。</summary>
    public virtual void IncrementSuccessfulDepletion() => SuccessfulDepletionCount++;

    /// <summary>入金失敗回数をカウントアップします。</summary>
    public virtual void IncrementFailedDepletion() => FailedDepletionCount++;

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを破棄します。</summary>
    /// <param name="disposing">マネージリソースを破棄する場合は true。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        // 外部から注入された inventory や hardwareStatusManager は、
        // このクラスの Dispose で破棄すべきではありません。
        disposed = true;
    }
}
