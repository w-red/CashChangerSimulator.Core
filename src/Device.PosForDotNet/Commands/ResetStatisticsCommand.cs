using CashChangerSimulator.Device.PosForDotNet.Coordination;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報リセット操作をカプセル化するコマンド。</summary>
public class ResetStatisticsCommand : IUposCommand
{
    private readonly string[] statistics;

    /// <summary><see cref="ResetStatisticsCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="statistics">リセット対象の統計情報プロパティ名の配列。</param>
    public ResetStatisticsCommand(string[] statistics)
    {
        this.statistics = statistics;
    }

    /// <summary>コマンドを実行します。</summary>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>コマンドを非同期で実行します。</summary>
    public Task ExecuteAsync()
    {
        // Reset logic can be added if needed
        return Task.CompletedTask;
    }

    /// <summary>コマンドの実行条件を検証します。</summary>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
