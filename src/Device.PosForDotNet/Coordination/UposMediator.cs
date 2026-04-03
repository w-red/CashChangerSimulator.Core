using System.Threading;
using Microsoft.PointOfService;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.PosForDotNet.Services;
using Microsoft.Extensions.Logging;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Exceptions;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>UPOS サービスオブジェクトの操作に関する共通の検証と結果処理を支援するクラス。</summary>
public class UposMediator : IUposMediator
{
    private readonly Lock _stateLock = new();
    private bool _isBusy;
    private int _resultCode;
    private int _resultCodeExtended;
    private int _asyncResultCode;
    private int _asyncResultCodeExtended;

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

    public int ResultCode
    {
        get { lock (_stateLock) return _resultCode; }
        private set
        {
            lock (_stateLock) _resultCode = value;
        }
    }

    public int ResultCodeExtended
    {
        get { lock (_stateLock) return _resultCodeExtended; }
        private set
        {
            lock (_stateLock) _resultCodeExtended = value;
        }
    }

    public bool DeviceEnabled { get; set; }
    public bool DataEventEnabled { get; set; }
    public bool Claimed { get; set; }
    public bool ClaimedByAnother
    {
        get => _sink?.ClaimedByAnother ?? false;
        set { if (_sink != null) _sink.ClaimedByAnother = value; }
    }
    public bool IsBusy
    {
        get { lock (_stateLock) return _isBusy; }
        set
        {
            lock (_stateLock)
            {
                _isBusy = value;
                if (_isBusy)
                {
                    _resultCode = (int)ErrorCode.Busy;
                }
                else
                {
                    // When busy is cleared, we assume back to success unless explicitly failed
                    _resultCode = (int)ErrorCode.Success;
                }
            }
            _sink?.SetAsyncProcessing(value);
        }
    }
    public int AsyncResultCode
    {
        get { lock (_stateLock) return _asyncResultCode; }
        set { lock (_stateLock) _asyncResultCode = value; }
    }
    public int AsyncResultCodeExtended
    {
        get { lock (_stateLock) return _asyncResultCodeExtended; }
        set { lock (_stateLock) _asyncResultCodeExtended = value; }
    }
    public bool SkipStateVerification { get; set; }

    public IUposEventSink? EventSink => _sink as IUposEventSink;

    /// <summary>検証規則に基づき、現在の状態をチェックします。</summary>
    /// <remarks>Open, Claim, Enable, Busy 等の状態を確認し、不適切な場合は例外をスローします。</remarks>
    /// <param name="mustBeClaimed">排他占有（Claim）が必要かどうか。</param>
    /// <param name="mustBeEnabled">デバイス有効化（Enabled）が必要かどうか。</param>
    /// <param name="mustNotBeBusy">ビジー状態であってはならないかどうか。</param>
    public void VerifyState(bool mustBeClaimed = true, bool mustBeEnabled = false, bool mustNotBeBusy = false)
    {
        if (SkipStateVerification) return;
        if (_sink == null) throw new InvalidOperationException("Mediator not initialized.");

        // [UPOS PRECEDENCE] Mandatory priority: Closed > NotClaimed > Claimed > Disabled > Busy
        // [UPOS 優先順位] 強制的な優先順位: Closed > NotClaimed > Claimed > Disabled > Busy

        // 1. Closed Check (ErrorCode.Closed)
        if (_sink.State == ControlState.Closed)
        {
            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        // 2. Claimed Check (ErrorCode.Claimed - i.e. Occupied by another)
        // [FIX] Always refresh the global lock status before checking ClaimedByAnother for precedence.
        _hardwareStatusManager?.RefreshClaimedStatus();
        if (_hardwareStatusManager != null && _sink != null)
        {
            _sink.ClaimedByAnother = _hardwareStatusManager.IsClaimedByAnother.Value;
        }

        if (mustBeClaimed && _sink != null && _sink.ClaimedByAnother)
        {
            throw new PosControlException("Device is claimed by another application.", ErrorCode.Claimed);
        }

        // 3. NotClaimed Check (ErrorCode.NotClaimed)
        if (mustBeClaimed && _sink != null && !_sink.Claimed)
        {
            throw new PosControlException("Device is not claimed.", ErrorCode.NotClaimed);
        }

        // 4. Disabled Check (ErrorCode.Disabled)
        if (mustBeEnabled && _sink != null && !_sink.DeviceEnabled)
        {
            throw new PosControlException("Device is disabled.", ErrorCode.Disabled);
        }

        // 5. Busy Check (ErrorCode.Busy)
        if (mustNotBeBusy && IsBusy)
        {
            throw new PosControlException("Device is busy.", ErrorCode.Busy);
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

    public virtual void SetSuccess()
    {
        lock (_stateLock)
        {
            _resultCode = (int)ErrorCode.Success;
            _resultCodeExtended = 0;
        }
    }

    public virtual void SetFailure(ErrorCode code, int codeEx = 0)
    {
        lock (_stateLock)
        {
            _resultCode = (int)code;
            _resultCodeExtended = codeEx;
        }
    }

    public virtual void HandleFailure(DeviceErrorCode code, int codeEx = 0)
    {
        // [FIX] Extended Code が未指定（0）の場合、DeviceErrorCode に応じたデフォルト値をセットする。
        if (codeEx == 0)
        {
            codeEx = code switch
            {
                DeviceErrorCode.NoInventory => 201, // OPOS_EXCH_NOMONEY
                DeviceErrorCode.Jammed => 118,      // OPOS_EXCH_JAM
                _ => 0
            };
        }

        ResultCode = (int)MapToErrorCode(code);
        ResultCodeExtended = codeEx;

        throw new PosControlException("Operation failed.", (ErrorCode)ResultCode, ResultCodeExtended);
    }

    public void FireEvent(EventArgs e)
    {
        if (EventSink == null) return;

        if (e is DataEventArgs de)
        {
            EventSink.QueueDataEvent(de);
        }
        else if (e is StatusUpdateEventArgs se)
        {
            EventSink.QueueStatusUpdateEvent(se);
        }
        else
        {
            EventSink.QueueEvent(e);
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

    public static ErrorCode MapToErrorCode(DeviceErrorCode deviceError)
    {
        return deviceError switch
        {
            DeviceErrorCode.Success => ErrorCode.Success,
            DeviceErrorCode.Failure => ErrorCode.Failure,
            DeviceErrorCode.Extended => ErrorCode.Extended,
            DeviceErrorCode.NoInventory => ErrorCode.Extended,
            DeviceErrorCode.Jammed => ErrorCode.Extended,
            DeviceErrorCode.Illegal => ErrorCode.Illegal,
            DeviceErrorCode.Busy => ErrorCode.Busy,
            DeviceErrorCode.NoService => ErrorCode.NoService,
            DeviceErrorCode.Disabled => ErrorCode.Disabled,
            _ => ErrorCode.Failure
        };
    }

    public static int MapToErrorCodeExtended(DeviceErrorCode deviceError)
    {
        return deviceError switch
        {
            DeviceErrorCode.NoInventory => 201, // POS for .NET standard for NoInventory
            DeviceErrorCode.Jammed => 118,      // Standard but implementation defined
            _ => 0
        };
    }
}
