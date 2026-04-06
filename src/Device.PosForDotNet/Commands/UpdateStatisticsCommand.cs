using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報更新操作をカプセル化するコマンド。</summary>
public class UpdateStatisticsCommand : IUposCommand
{
    private readonly Statistic[] statistics;

    /// <summary><see cref="UpdateStatisticsCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="statistics">更新対象の統計情報の配列。</param>
    public UpdateStatisticsCommand(Statistic[] statistics)
    {
        this.statistics = statistics;
    }

    /// <summary>コマンドを実行します。</summary>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>コマンドを非同期で実行します。</summary>
    public Task ExecuteAsync()
    {
        // Simulator doesn't support external update, but follows UPOS protocol
        return Task.CompletedTask;
    }

    /// <summary>コマンドの実行条件を検証します。</summary>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
