using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>
/// サービスオブジェクトの各コンポーネント間の通信を仲介するインターフェース。
/// </summary>
public interface IUposMediator
{
    void SetSuccess();
    void SetFailure(ErrorCode code, int codeEx = 0);
    void VerifyState(bool mustBeClaimed = true, bool mustBeEnabled = false, bool mustNotBeBusy = false);
    
    bool DeviceEnabled { get; set; }
    bool DataEventEnabled { get; set; }
    bool Claimed { get; set; }
    
    bool SkipStateVerification { get; set; }
    bool IsBusy { get; set; }
    int AsyncResultCode { get; set; }
    int AsyncResultCodeExtended { get; set; }
    
    // 他のコンポーネントからの通知
    void HandleDispenseResult(ErrorCode code, int codeEx, bool wasAsync);

    /// <summary>コマンドを実行し、結果を反映します。</summary>
    void Execute(IUposCommand command);
}
