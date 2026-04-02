using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.PosForDotNet.Services;
using Microsoft.Extensions.Logging;
using CashChangerSimulator.Device.Virtual.Services;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Exceptions;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>UPOS サービスオブジェクトの操作に関する共通の検証と結果処理を支援するクラス。</summary>
public class UposMediator : IUposMediator
{
    private bool _isBusy;
    private ICashChangerStatusSink? _sink;
    private ILogger? _logger;
    private StatusCoordinator? _coordinator;
    private HardwareStatusManager? _hardwareStatusManager;

    /// <summary>依存関係を指定せずに初期化します（後で Initialize を呼ぶ必要があります）。</summary>
    public UposMediator()
    {
    }

    /// <inheritdoc/>
    public void Initialize(ICashChangerStatusSink sink, ILogger logger, StatusCoordinator coordinator, HardwareStatusManager hardwareStatusManager)
    {
        _sink = sink;
        _logger = logger;
        _coordinator = coordinator;
        _hardwareStatusManager = hardwareStatusManager;
    }

    public int ResultCode { get; private set; }
    public int ResultCodeExtended { get; private set; }

    public bool DeviceEnabled { get; set; }
    public bool DataEventEnabled { get; set; }
    public bool Claimed { get; set; }
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            if (_isBusy)
            {
                ResultCode = (int)ErrorCode.Busy;
            }
            else
            {
                // When busy is cleared, we assume back to success unless explicitly failed
                ResultCode = (int)ErrorCode.Success;
            }
            _sink?.SetAsyncProcessing(value);
        }
    }
    public int AsyncResultCode { get; set; }
    public int AsyncResultCodeExtended { get; set; }
    public bool SkipStateVerification { get; set; }

    public IUposEventSink? EventSink => _sink as IUposEventSink;

    /// <inheritdoc/>
    public void VerifyState(bool mustBeClaimed = true, bool mustBeEnabled = false, bool mustNotBeBusy = false)
    {
        if (SkipStateVerification) return;
        if (_sink == null) throw new InvalidOperationException("Mediator not initialized.");

        // NOTE: We assume the sink provides the necessary state information.
        // In this implementation, we use the SO's state if available via cast.
        if (_sink is not CashChangerBasic so)
        {
             // Fallback or simplified check if not a full SO
             return;
        }

        if (so.State == ControlState.Closed)
        {
            throw new PosControlException("Device is not open.", ErrorCode.Closed);
        }

        if (mustBeClaimed && _hardwareStatusManager != null)
        {
            _hardwareStatusManager.RefreshClaimedStatus();
            if (_hardwareStatusManager.IsClaimedByAnother.Value)
            {
                throw new PosControlException("Device is claimed by another application.", ErrorCode.Claimed);
            }
        }

        if (mustBeClaimed && !so.Claimed)
        {
            throw new PosControlException("Device is not claimed.", ErrorCode.NotClaimed);
        }

        if (mustBeEnabled && !so.DeviceEnabled)
        {
            throw new PosControlException("Device is not enabled.", ErrorCode.Disabled);
        }

        if (mustNotBeBusy)
        {
            ThrowIfBusy(IsBusy);
        }
    }

    public static void ThrowIfBusy(bool asyncProcessing)
    {
        if (asyncProcessing)
        {
            throw new PosControlException("Device is busy with an asynchronous operation.", ErrorCode.Busy);
        }
    }

    public static void ThrowIfDepositInProgress(bool inProgress)
    {
        if (inProgress)
        {
            throw new PosControlException("Deposit in progress.", ErrorCode.Illegal);
        }
    }

    public void SetSuccess()
    {
        ResultCode = (int)ErrorCode.Success;
        ResultCodeExtended = 0;
    }

    public void SetFailure(ErrorCode code, int codeEx = 0)
    {
        ResultCode = (int)code;
        ResultCodeExtended = codeEx;
    }

    public void HandleDispenseResult(ErrorCode code, int codeEx, bool wasAsync)
    {
        if (wasAsync)
        {
            _logger?.LogInformation("Async dispense finished with code: {Code}, codeEx: {CodeEx}", code, codeEx);
            AsyncResultCode = (int)code;
            AsyncResultCodeExtended = codeEx;
            IsBusy = false; 
            SetFailure(code, codeEx); // Overwrite ResultCode with actual result
            _sink?.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.AsyncFinished));
        }
        else
        {
            SetFailure(code, codeEx);
        }
    }

    public void Execute(IUposCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            command.Verify(this);
            command.Execute();
            
            // For asynchronous commands, IsBusy will be set to true inside Execute().
            // In that case, we should NOT call SetSuccess() immediately as the method return value
            // is SUCCESS but the device state is BUSY.
            if (!IsBusy)
            {
                SetSuccess();
            }
        }
        catch (PosControlException ex)
        {
            SetFailure(ex.ErrorCode, ex.ErrorCodeExtended);
            throw;
        }
        catch (DeviceException ex)
        {
            SetFailure((ErrorCode)ex.ErrorCode, ex.ErrorCodeExtended);
            throw new PosControlException(ex.Message, (ErrorCode)ex.ErrorCode, ex.ErrorCodeExtended, ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during command execution.");

            // Extract ErrorCode from Exception if it has a property or is a known type
            var errorCode = ErrorCode.Failure;
            var errorCodeExtended = 0;

            if (ex.GetType().GetProperty("ErrorCode")?.GetValue(ex) is int codeAsInt)
            {
                errorCode = (ErrorCode)codeAsInt;
            }
            if (ex.GetType().GetProperty("ErrorCodeExtended")?.GetValue(ex) is int codeExAsInt)
            {
                errorCodeExtended = codeExAsInt;
            }

            SetFailure(errorCode, errorCodeExtended);
            throw new PosControlException(ex.Message, errorCode, errorCodeExtended, ex);
        }
    }
}
