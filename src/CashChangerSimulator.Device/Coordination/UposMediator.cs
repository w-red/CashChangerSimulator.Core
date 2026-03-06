using Microsoft.PointOfService;
using CashChangerSimulator.Core.Opos;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>UPOS サービスオブジェクトの操作に関する共通の検証と結果処理を支援するクラス。Mediator パターンを実装します。</summary>
public class UposMediator : IUposMediator
{
    private readonly SimulatorCashChanger _so;

    public UposMediator(SimulatorCashChanger so)
    {
        _so = so ?? throw new ArgumentNullException(nameof(so));
    }

    public int ResultCode { get; private set; }
    public int ResultCodeExtended { get; private set; }

    public bool DeviceEnabled { get; set; }
    public bool DataEventEnabled { get; set; }
    public bool Claimed { get; set; }
    public bool IsBusy { get; set; }
    public int AsyncResultCode { get; set; }
    public int AsyncResultCodeExtended { get; set; }

    /// <summary>UPOS ライフサイクルの状態を検証します。</summary>
    public void VerifyState(bool skip, bool mustBeClaimed = true, bool mustBeEnabled = false, bool mustNotBeBusy = false)
    {
        if (skip) return;

        if (_so == null) throw new InvalidOperationException("_so is null in UposMediator.VerifyState");

        ControlState currentState;
        try
        {
            currentState = _so.State;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Exception accessing _so.State in VerifyState. SO type: {_so.GetType().Name}", ex);
        }

        if (currentState == ControlState.Closed)
        {
            throw new PosControlException("Device is not open.", ErrorCode.Closed);
        }

        if (mustBeClaimed && !_so.Claimed)
        {
            throw new PosControlException("Device is not claimed.", ErrorCode.Illegal);
        }

        if (mustBeEnabled && !_so.DeviceEnabled)
        {
            throw new PosControlException("Device is not enabled.", ErrorCode.Disabled);
        }

        if (mustNotBeBusy)
        {
            ThrowIfBusy(IsBusy);
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

    /// <summary>操作が成功したことを記録し、ResultCode を更新します。</summary>
    public void SetSuccess()
    {
        ResultCode = (int)ErrorCode.Success;
        ResultCodeExtended = 0;
    }

    /// <summary>操作が失敗したことを記録し、ResultCode を更新します。</summary>
    public void SetFailure(ErrorCode code, int codeEx = 0)
    {
        ResultCode = (int)code;
        ResultCodeExtended = codeEx;
    }

    /// <summary>払い出し操作の結果を処理し、SOの状態とイベントを更新します。</summary>
    public void HandleDispenseResult(ErrorCode code, int codeEx, bool wasAsync)
    {
        SetFailure(code, codeEx);
        if (wasAsync)
        {
            AsyncResultCode = (int)code;
            AsyncResultCodeExtended = codeEx;
            IsBusy = false;
            _so.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.AsyncFinished));
        }
    }

    /// <summary>コマンドを実行し、結果を反映します。</summary>
    public void Execute(IUposCommand command, bool skipStateVerification)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));

        try
        {
            command.Verify(this, skipStateVerification);
            command.Execute();
            SetSuccess();
        }
        catch (PosControlException ex)
        {
            SetFailure(ex.ErrorCode, ex.ErrorCodeExtended);
            throw; // Re-throw to inform the caller/service object
        }
        catch (Exception)
        {
            SetFailure(ErrorCode.Failure);
            throw;
        }
    }
}
