using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Facades;

/// <summary>UPOS の診断・ヘルスチェック操作を統合的に処理する <see cref="DiagnosticsFacade"/>。</summary>
/// <param name="diagnosticController">診断ロジックを制御する <see cref="DiagnosticController"/>。</param>
/// <param name="mediator">コマンド実行を仲介する <see cref="IUposMediator"/>。</param>
/// <remarks>
/// デバイスのヘルスチェック（<see cref="CheckHealth"/>）、統計情報の取得・更新・リセット（<see cref="RetrieveStatistics"/>等）など、
/// デバイスのメンテナンスと診断に関連する操作を集約します。
/// </remarks>
public class DiagnosticsFacade(DiagnosticController diagnosticController, IUposMediator mediator)
{

    /// <summary>デバイスの健康状態を確認します。</summary>
    /// <returns>ヘルスチェック結果を示す文字列。</returns>
    public string CheckHealth(HealthCheckLevel level)
    {
        var command = new CheckHealthCommand(diagnosticController, level);
        mediator.Execute(command);
        return command.Result;
    }
 
    /// <summary>統計情報を取得します。</summary>
    /// <remarks>
    /// 指定された統計情報カテゴリを XML 形式で取得します。
    /// </remarks>
    public string RetrieveStatistics(string[] statistics)
    {
        var command = new RetrieveStatisticsCommand(diagnosticController, statistics);
        mediator.Execute(command);
        return command.Result;
    }
 
    /// <summary>統計情報を更新します。</summary>
    public void UpdateStatistics(Statistic[] statistics)
    {
        mediator.Execute(new UpdateStatisticsCommand(statistics));
    }
 
    /// <summary>統計情報をリセットします。</summary>
    public void ResetStatistics(string[] statistics)
    {
        mediator.Execute(new ResetStatisticsCommand(statistics));
    }
 
    /// <summary>成功した出金操作の数を増加させます。</summary>
    public void IncrementSuccessfulDepletion()
    {
        diagnosticController.IncrementSuccessfulDepletion();
    }
}
