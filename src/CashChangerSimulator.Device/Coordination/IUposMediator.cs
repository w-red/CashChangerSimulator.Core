using Microsoft.PointOfService;
using CashChangerSimulator.Device.Services;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>サービスオブジェクトの各コンポーネント間の通信を仲介するインターフェース。</summary>
/// <remarks>
/// Mediator パターンを定義し、デバイスの状態検証、結果設定、およびコマンドの実行を一貫したインターフェースで提供します。
/// </remarks>
public interface IUposMediator
{
    /// <summary>操作の成功を記録します。</summary>
    /// <remarks>ResultCode を Success に設定します。</remarks>
    void SetSuccess();

    /// <summary>操作の失敗を記録します。</summary>
    /// <remarks>ResultCode と ResultCodeExtended に指定されたエラー情報を設定します。</remarks>
    void SetFailure(ErrorCode code, int codeEx = 0);

    /// <summary>現在のデバイス状態が操作可能か検証します。</summary>
    /// <remarks>Open, Claim, Enable, Busy 等の状態を確認し、不適切な場合は例外をスローします。</remarks>
    void VerifyState(bool mustBeClaimed = true, bool mustBeEnabled = false, bool mustNotBeBusy = false);
    
    /// <summary>デバイスが有効（Enable）かどうか。</summary>
    bool DeviceEnabled { get; set; }

    /// <summary>データイベントを通知するかどうか。</summary>
    bool DataEventEnabled { get; set; }

    /// <summary>デバイスが占有（Claim）されているかどうか。</summary>
    bool Claimed { get; set; }
    
    /// <summary>状態検証（VerifyState）をスキップするかどうか。</summary>
    bool SkipStateVerification { get; set; }

    /// <summary>非同期処理中かどうか。</summary>
    bool IsBusy { get; set; }

    /// <summary>非同期操作の完了コード。</summary>
    int AsyncResultCode { get; set; }

    /// <summary>非同期操作の拡張完了コード。</summary>
    int AsyncResultCodeExtended { get; set; }

    /// <summary>イベント通知の送り先となるシンク。</summary>
    IUposEventSink EventSink { get; }
    
    // 他のコンポーネントからの通知
    /// <summary>払い出し操作の結果を処理します。</summary>
    /// <remarks>成功・失敗の記録と、必要に応じてイベントのトリガーを行います。</remarks>
    void HandleDispenseResult(ErrorCode code, int codeEx, bool wasAsync);

    /// <summary>コマンドを実行し、結果を反映します。</summary>
    void Execute(IUposCommand command);
}
