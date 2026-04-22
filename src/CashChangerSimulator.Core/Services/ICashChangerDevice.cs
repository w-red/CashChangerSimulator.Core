using CashChangerSimulator.Core.Models;
using PosSharp.Abstractions;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using R3;

namespace CashChangerSimulator.Core.Services;

/// <summary>現金入出金機の基本操作と状態監視を定義する抽象インターフェース。</summary>
public interface ICashChangerDevice : IDisposable
{
    /// <summary>デバイスが現在ビジー状態かどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsBusy { get; }

    /// <summary>デバイスの現在の制御状態。</summary>
    ReadOnlyReactiveProperty<PosSharp.Abstractions.ControlState> State { get; }

    /// <summary>データイベント通知。</summary>
    Observable<PosSharp.Abstractions.UposDataEventArgs> DataEvents { get; }

    /// <summary>エラーイベント通知。</summary>
    Observable<PosSharp.Abstractions.UposErrorEventArgs> ErrorEvents { get; }

    /// <summary>ステータス更新イベント通知。</summary>
    Observable<PosSharp.Abstractions.UposStatusUpdateEventArgs> StatusUpdateEvents { get; }

    /// <summary>ダイレクトIOイベント通知。</summary>
    Observable<DeviceDirectIOEventArgs> DirectIOEvents { get; }

    /// <summary>出力完了イベント通知。</summary>
    Observable<PosSharp.Abstractions.UposOutputCompleteEventArgs> OutputCompleteEvents { get; }

    /// <summary>デバイスを非同期でオープンします。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task OpenAsync();

    /// <summary>デバイスを非同期でクローズします。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task CloseAsync();

    /// <summary>デバイスの排他権を非同期で取得します。</summary>
    /// <param name="timeout">タイムアウト(ミリ秒)。</param>
    /// <returns>完了を示すタスク。</returns>
    Task ClaimAsync(int timeout);

    /// <summary>デバイスの排他権を非同期で解放します。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task ReleaseAsync();

    /// <summary>デバイスを非同期で有効化します。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task EnableAsync();

    /// <summary>デバイスを非同期で無効化します。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task DisableAsync();

    /// <summary>預入(Deposit)処理を開始します。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task BeginDepositAsync();

    /// <summary>預入されている金額を確定させます。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task FixDepositAsync();

    /// <summary>預入処理を一時停止または再開します。</summary>
    /// <param name="control">一時停止または再開。</param>
    /// <returns>完了を示すタスク。</returns>
    Task PauseDepositAsync(DeviceDepositPause control);

    /// <summary>投入された現金を返却し、入金処理を終了します。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task RepayDepositAsync();

    /// <summary>預入処理を終了(収納または返却)します。</summary>
    /// <param name="action">終了アクション。</param>
    /// <returns>完了を示すタスク。</returns>
    Task EndDepositAsync(DepositAction action);

    /// <summary>指定された金額を払い出します。</summary>
    /// <param name="amount">払い出す金額。</param>
    /// <returns>完了を示すタスク。</returns>
    Task DispenseChangeAsync(int amount);

    /// <summary>指定された金種と枚数を払い出します。</summary>
    /// <param name="counts">払い出す金種と枚数のリスト。</param>
    /// <returns>完了を示すタスク。</returns>
    Task DispenseCashAsync(IEnumerable<CashDenominationCount> counts);

    /// <summary>現在の在庫情報を読み取ります。</summary>
    /// <returns>現在の在庫。</returns>
    Task<Inventory> ReadInventoryAsync();

    /// <summary>在庫枚数を調整します。</summary>
    /// <param name="counts">調整内容。</param>
    /// <returns>完了を示すタスク。</returns>
    Task AdjustInventoryAsync(IEnumerable<CashDenominationCount> counts);

    /// <summary>現金を回収庫へ移動(パージ)します。</summary>
    /// <returns>完了を示すタスク。</returns>
    Task PurgeCashAsync();

    /// <summary>デバイスの健康診断(自己診断)を実行します。</summary>
    /// <param name="level">診断レベル。</param>
    /// <returns>診断結果メッセージ。</returns>
    Task<string> CheckHealthAsync(PosSharp.Abstractions.HealthCheckLevel level);
}
