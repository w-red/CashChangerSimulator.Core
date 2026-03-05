using Microsoft.PointOfService;
using CashChangerSimulator.Core.Opos;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>UPOS サービスオブジェクトの操作に関する共通の検証と結果処理を支援するクラス。</summary>
public class UposOperationHelper
{
    private readonly SimulatorCashChanger _so;

    public UposOperationHelper(SimulatorCashChanger so)
    {
        _so = so;
    }

    /// <summary>UPOS ライフサイクルの状態を検証します。</summary>
    public void VerifyState(bool skip, bool mustBeClaimed = true)
    {
        // Even if we skip real execution, the logic state must be correct.
        if (_so.State == ControlState.Closed)
        {
            throw new PosControlException("Device is not open.", ErrorCode.Closed);
        }

        if (mustBeClaimed && !_so.Claimed)
        {
            throw new PosControlException("Device is not claimed.", ErrorCode.Illegal);
        }
    }

    /// <summary>デバイスが非同期処理中でないことを確認します。</summary>
    public static void ThrowIfBusy(bool asyncProcessing)
    {
        if (asyncProcessing)
        {
            throw new PosControlException("Device is busy with an asynchronous operation.", ErrorCode.Busy);
        }
    }

    /// <summary>入金処理が進行中でないことを確認します。</summary>
    public static void ThrowIfDepositInProgress(bool inProgress)
    {
        if (inProgress)
        {
            throw new PosControlException(
                "Cash cannot be dispensed because cash acceptance is in progress.",
                ErrorCode.Illegal);
        }
    }

    /// <summary>払い出し操作の結果を処理し、SOの状態とイベントを更新します。</summary>
    public void HandleDispenseResult(ErrorCode code, int codeEx, bool wasAsync)
    {
        _so.ResultCode = (int)code;
        _so.ResultCodeExtended = codeEx;
        if (wasAsync)
        {
            _so.SetAsyncProcessing(false);
            _so.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.AsyncFinished));
        }
    }
}
