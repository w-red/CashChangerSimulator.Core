using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.Core.Managers;

/// <summary>ハードウェアの状態（ジャム、接続、エラー等）を操作するためのインターフェース。</summary>
public interface IHardwareStatusInput
{
    /// <summary>占有状態を操作するためのプロパティ。</summary>
    ReactiveProperty<bool> IsClaimedByAnother { get; }

    /// <summary>デバイスの有効状態を操作するためのプロパティ。</summary>
    ReactiveProperty<bool> DeviceEnabled { get; }

    /// <summary>ジャム状態を操作するためのプロパティ。</summary>
    ReactiveProperty<bool> IsJammed { get; }

    /// <summary>ジャム発生箇所を操作するためのプロパティ。</summary>
    ReactiveProperty<JamLocation> CurrentJamLocation { get; }

    /// <summary>バリデーションエラー状態を操作するためのプロパティ。</summary>
    ReactiveProperty<bool> IsOverlapped { get; }

    /// <summary>デバイスエラー状態を操作するためのプロパティ。</summary>
    ReactiveProperty<bool> IsDeviceError { get; }

    /// <summary>接続状態を操作するためのプロパティ。</summary>
    ReactiveProperty<bool> IsConnected { get; }

    /// <summary>回収庫取り外し状態を操作するためのプロパティ。</summary>
    ReactiveProperty<bool> IsCollectionBoxRemoved { get; }

    /// <summary>エラーコードを操作するためのプロパティ。</summary>
    ReactiveProperty<int?> CurrentErrorCode { get; }

    /// <summary>拡張エラーコードを操作するためのプロパティ。</summary>
    ReactiveProperty<int> CurrentErrorCodeExtended { get; }

    /// <summary>すべての状態をリセットするためのトリガー。</summary>
    Subject<Unit> ResetTrigger { get; }
}
