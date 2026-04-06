using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報取得操作をカプセル化するコマンド。</summary>
public class RetrieveStatisticsCommand : IUposCommand
{
    private readonly DiagnosticController controller;
    private readonly string[] statistics;

    /// <summary><see cref="RetrieveStatisticsCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="controller">診断コントローラー。</param>
    /// <param name="statistics">取得対象の統計情報プロパティ名の配列。</param>
    public RetrieveStatisticsCommand(DiagnosticController controller, string[] statistics)
    {
        this.controller = controller;
        this.statistics = statistics;
        Result = string.Empty;
    }

    /// <summary>診断の実行結果を取得します。</summary>
    public string Result { get; private set; }

    /// <summary>コマンドを実行します。</summary>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>コマンドを非同期で実行します。</summary>
    public Task ExecuteAsync()
    {
        Result = controller.RetrieveStatistics(statistics);
        return Task.CompletedTask;
    }

    /// <summary>コマンドの実行条件を検証します。</summary>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
