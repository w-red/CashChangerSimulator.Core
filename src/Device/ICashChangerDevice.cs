using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.Device;

/// <summary>現金入出金機の基本操作と状態監視を定義する抽象インターフェース。</summary>
public interface ICashChangerDevice : IDisposable
{
    /// <summary>デバイスが現在ビジー状態かどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsBusy { get; }

    /// <summary>デバイスの現在の制御状態。</summary>
    ReadOnlyReactiveProperty<DeviceControlState> State { get; }

    /// <summary>デバイスを非同期でオープンします。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task OpenAsync();

    /// <summary>デバイスを非同期でクローズします。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task CloseAsync();

    /// <summary>預入（Deposit）処理を開始します。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task BeginDepositAsync();

    /// <summary>預入されている金額を確定させます。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task FixDepositAsync();

    /// <summary>預入処理を終了（収納または返却）します。</summary>
    /// <param name="action">終了アクション。</param>
    /// <returns>完了を示すタスク。</returns>
    Task EndDepositAsync(DepositAction action);

    /// <summary>指定された金額を払い出します。</summary>
    /// <param name="amount">払い出す金額。</param>
    /// <returns>完了を示すタスク。</returns>
    Task DispenseAsync(int amount);

    /// <summary>指定された金種と枚数を払い出します。</summary>
    /// <param name="counts">払い出す金種と枚数のリスト。</param>
    /// <returns>完了を示すタスク。</returns>
    Task DispenseAsync(IEnumerable<CashDenominationCount> counts);

    /// <summary>現在の在庫情報を読み取ります。</summary>
    /// <returns>現在の在庫。</returns>
    Task<Inventory> ReadInventoryAsync();

    /// <summary>在庫枚数を調整します。</summary>
    /// <param name="counts">調整内容。</param>
    /// <returns>完了を示すタスク。</returns>
    Task AdjustInventoryAsync(IEnumerable<CashDenominationCount> counts);

    /// <summary>デバイスの健康診断（自己診断）を実行します。</summary>
    /// <returns>診断結果メッセージ。</returns>
    Task<string> CheckHealthAsync();
}
