using CashChangerSimulator.Core.Models;
using PosSharp.Abstractions;
using R3;

namespace CashChangerSimulator.Core.Managers;

/// <summary>ハードウェアの状態(ジャム、接続、エラー等)を公開・監視するための読み取り専用インターフェース。</summary>
public interface IHardwareStatus
{
    /// <summary>他のプロセスによってデバイスが占有されているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsClaimedByAnother { get; }

    /// <summary>デバイスが有効化されているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> DeviceEnabled { get; }

    /// <summary>ジャムが発生しているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsJammed { get; }

    /// <summary>ジャムが発生している場所。</summary>
    ReadOnlyReactiveProperty<JamLocation> CurrentJamLocation { get; }

    /// <summary>バリデーションエラー(重なり等)が発生しているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsOverlapped { get; }

    /// <summary>一般的なデバイスエラーが発生しているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsDeviceError { get; }

    /// <summary>デバイスが接続されているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsConnected { get; }

    /// <summary>回収庫が取り外されているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsCollectionBoxRemoved { get; }

    /// <summary>発生中のエラーコード。</summary>
    ReadOnlyReactiveProperty<int?> CurrentErrorCode { get; }

    /// <summary>発生中の拡張エラーコード。</summary>
    ReadOnlyReactiveProperty<int> CurrentErrorCodeExtended { get; }

    /// <summary>デバイスが正常な状態(主要なエラーが発生していない状態)かどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsNormal { get; }

    /// <summary>通常口に紙幣が残っているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsBillRemainingNormal { get; }

    /// <summary>通常口に硬貨が残っているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsCoinRemainingNormal { get; }

    /// <summary>回収口に紙幣が残っているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsBillRemainingCollection { get; }

    /// <summary>回収口に硬貨が残っているかどうか。</summary>
    ReadOnlyReactiveProperty<bool> IsCoinRemainingCollection { get; }

    /// <summary>指定された出金口に現在留まっている現金の枚数内訳を取得します。</summary>
    /// <param name="port">出金口。</param>
    /// <returns>金種別の枚数内訳。</returns>
    IReadOnlyDictionary<DenominationKey, int> GetExitPortCounts(ExitPort port);

    /// <summary>ステータス更新通知イベントのストリーム。</summary>
    Observable<PosSharp.Abstractions.UposStatusUpdateEventArgs> StatusUpdateEvents { get; }
}
