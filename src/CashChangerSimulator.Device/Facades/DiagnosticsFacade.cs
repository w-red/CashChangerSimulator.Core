using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Facades;

/// <summary>UPOS の診断・ヘルスチェック操作を統合的に処理する Facade。</summary>
/// <remarks>デバイスのヘルスチェック・統計情報の取得・更新・リセットを集約します。</remarks>
public class DiagnosticsFacade
{
    private readonly DiagnosticController _diagnosticController;
    private readonly IUposMediator _mediator;

    /// <summary>新しいインスタンスを初期化します。</summary>
    public DiagnosticsFacade(DiagnosticController diagnosticController, IUposMediator mediator)
    {
        _diagnosticController = diagnosticController ?? throw new ArgumentNullException(nameof(diagnosticController));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>デバイスの健康状態を確認します。</summary>
    /// <returns>ヘルスチェック結果を示す文字列。</returns>
    public string CheckHealth(HealthCheckLevel level)
    {
        var command = new CheckHealthCommand(_diagnosticController, level);
        _mediator.Execute(command);
        return command.Result;
    }
 
    /// <summary>統計情報を取得します。</summary>
    /// <remarks>
    /// 指定された統計情報カテゴリを XML 形式で取得します。
    /// </remarks>
    public string RetrieveStatistics(string[] statistics)
    {
        var command = new RetrieveStatisticsCommand(_diagnosticController, statistics);
        _mediator.Execute(command);
        return command.Result;
    }
 
    /// <summary>統計情報を更新します。</summary>
    public void UpdateStatistics(Statistic[] statistics)
    {
        _mediator.Execute(new UpdateStatisticsCommand(statistics));
    }
 
    /// <summary>統計情報をリセットします。</summary>
    public void ResetStatistics(string[] statistics)
    {
        _mediator.Execute(new ResetStatisticsCommand(statistics));
    }

    /// <summary>成功した出金操作の数を増加させます。</summary>
    public void IncrementSuccessfulDepletion()
    {
        _diagnosticController.IncrementSuccessfulDepletion();
    }
}
