using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.PosForDotNet.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>サービスオブジェクトの各コンポーネント間の通信を仲介するインターフェース。</summary>
/// <remarks>Mediator パターンを定義し、デバイスの状態検証、結果設定、およびコマンドの実行を一貫したインターフェースで提供します。</remarks>
public interface IUposMediator
{
    /// <summary>メディエータを初期化します。</summary>
    void Initialize(ICashChangerStatusSink sink, ILogger logger, StatusCoordinator coordinator, HardwareStatusManager hardwareStatusManager);

    /// <summary>操作の成功を記録します。</summary>
    /// <remarks>ResultCode を Success に設定します。</remarks>
    void SetSuccess();

    /// <summary>操作の失敗を記録します。</summary>
    /// <remarks>ResultCode と ResultCodeExtended に指定されたエラー情報を設定します。</remarks>
    void SetFailure(ErrorCode code, int codeEx = 0);

    /// <summary>現在の操作の完了コードを取得します。</summary>
    int ResultCode { get; }

    /// <summary>現在の操作の拡張完了コードを取得します。</summary>
    int ResultCodeExtended { get; }

    /// <summary>現在のデバイス状態が操作可能か検証します。</summary>
    /// <remarks>Open, Claim, Enable, Busy 等の状態を確認し、不適切な場合は例外をスローします。</remarks>
    void VerifyState(bool mustBeClaimed = true, bool mustBeEnabled = false, bool mustNotBeBusy = false);

    /// <summary>デバイスが有効(Enable)かどうかを取得または設定します。</summary>
    bool DeviceEnabled { get; set; }

    /// <summary>データイベントを通知するかどうかを取得または設定します。</summary>
    bool DataEventEnabled { get; set; }

    /// <summary>デバイスが占有(Claim)されているかどうかを取得または設定します。</summary>
    bool Claimed { get; set; }

    /// <summary>他者によってデバイスが占有されているかどうかを取得または設定します。</summary>
    bool ClaimedByAnother { get; set; }

    /// <summary>状態検証(VerifyState)をスキップするかどうかを取得または設定します。</summary>
    bool SkipStateVerification { get; set; }

    /// <summary>非同期処理中かどうかを取得または設定します。</summary>
    bool IsBusy { get; set; }

    /// <summary>非同期操作の完了コードを取得または設定します。</summary>
    int AsyncResultCode { get; set; }

    /// <summary>非同期操作の拡張完了コードを取得または設定します。</summary>
    int AsyncResultCodeExtended { get; set; }

    /// <summary>イベント通知の送り先となるシンクを取得します。</summary>
    IUposEventSink? EventSink { get; }

    // 他のコンポーネントからの通知

    /// <summary>IsBusy の変更を通知するストリームを取得します。</summary>
    R3.ReadOnlyReactiveProperty<bool> IsBusyProperty { get; }

    /// <summary>コマンドを実行し、結果を反映します。</summary>
    void Execute(IUposCommand command);
}
