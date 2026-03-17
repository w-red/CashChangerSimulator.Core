namespace CashChangerSimulator.Core.Services;

/// <summary>釣銭機ハードウェアの動作をシミュレートするインターフェース。</summary>
public interface IDeviceSimulator : IDisposable
{
    /// <summary>払い出し動作の遅延をシミュレートします。</summary>
    /// <param name="ct">キャンセル・トークン。</param>
    /// <returns>待機タスク。</returns>
    Task SimulateDispenseAsync(CancellationToken ct = default);
}
