using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>UPOS サービスオブジェクトの操作に関する共通の検証と結果処理を支援するクラス。</summary>
public class UposMediator : IUposMediator
{
    private readonly Lock stateLock = new();
    private readonly ReactiveProperty<bool> isBusyProperty = new(false);

    private ICashChangerStatusSink? sink;
    private ILogger? logger;
    private HardwareStatusManager? hardwareStatusManager;

    /// <summary>Initializes a new instance of the <see cref="UposMediator"/> class.依存関係を指定せずに初期化します(後で Initialize を呼ぶ必要があります)。</summary>
    public UposMediator()
    {
    }

    /// <inheritdoc/>
    public void Initialize(ICashChangerStatusSink sink, ILogger logger, StatusCoordinator coordinator, HardwareStatusManager hardwareStatusManager)
    {
        this.sink = sink;
        this.logger = logger;
        this.hardwareStatusManager = hardwareStatusManager;
    }

    /// <inheritdoc/>
    public int ResultCode
    {
        get
        {
            lock (stateLock)
            {
                return field;
            }
        }
        private set
        {
            lock (stateLock)
            {
                field = value;
            }
        }
    }

    /// <inheritdoc/>
    public int ResultCodeExtended
    {
        get
        {
            lock (stateLock)
            {
                return field;
            }
        }
        private set
        {
            lock (stateLock)
            {
                field = value;
            }
        }
    }

    /// <inheritdoc/>
    public bool DeviceEnabled { get; set; }

    /// <inheritdoc/>
    public bool DataEventEnabled { get; set; }

    /// <inheritdoc/>
    public bool Claimed { get; set; }

    /// <inheritdoc/>
    public bool ClaimedByAnother
    {
        get => sink?.ClaimedByAnother ?? false;
        set
        {
            if (sink is not null) sink.ClaimedByAnother = value;
        }
    }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsBusyProperty => isBusyProperty;

    /// <inheritdoc/>
    public bool IsBusy
    {
        get
        {
            lock (stateLock)
            {
                return field;
            }
        }
        set
        {
            lock (stateLock)
            {
                if (field == value) return;
                field = value;
                ResultCode = (int)(value ? ErrorCode.Busy : ErrorCode.Success);
            }

            isBusyProperty.Value = value;
            sink?.SetAsyncProcessing(value);
        }
    }

    /// <inheritdoc/>
    public int AsyncResultCode
    {
        get
        {
            lock (stateLock)
            {
                return field;
            }
        }
        set
        {
            lock (stateLock)
            {
                field = value;
            }
        }
    }

    /// <inheritdoc/>
    public int AsyncResultCodeExtended
    {
        get
        {
            lock (stateLock)
            {
                return field;
            }
        }
        set
        {
            lock (stateLock)
            {
                field = value;
            }
        }
    }

    /// <inheritdoc/>
    public bool SkipStateVerification { get; set; }

    /// <inheritdoc/>
    public IUposEventSink? EventSink => sink as IUposEventSink;

    /// <summary>検証規則に基づき、現在の状態をチェックします。</summary>
    /// <remarks>Open, Claim, Enable, Busy 等の状態を確認し、不適切な場合は例外をスローします。</remarks>
    /// <param name="mustBeClaimed">排他占有(Claim)が必要かどうか。</param>
    /// <param name="mustBeEnabled">デバイス有効化(Enabled)が必要かどうか。</param>
    /// <param name="mustNotBeBusy">ビジー状態であってはならないかどうか。</param>
    public void VerifyState(bool mustBeClaimed = true, bool mustBeEnabled = false, bool mustNotBeBusy = false)
    {
        if (SkipStateVerification)
        {
            return;
        }

        if (sink == null)
        {
            throw new InvalidOperationException("Mediator not initialized.");
        }

        // [UPOS PRECEDENCE] Mandatory priority: Closed > NotClaimed > Claimed > Disabled > Busy
        // [UPOS 優先順位] 強制的な優先順位: Closed > NotClaimed > Claimed > Disabled > Busy

        // 1. Closed Check (ErrorCode.Closed)
        if (sink.State == ControlState.Closed)
        {
            throw new PosControlException("Device is closed.", ErrorCode.Closed);
        }

        // 2. Claimed Check (ErrorCode.Claimed - i.e. Occupied by another)
        // [FIX] Always refresh the global lock status before checking ClaimedByAnother for precedence.
        hardwareStatusManager?.RefreshClaimedStatus();
        if (hardwareStatusManager != null && sink != null)
        {
            sink.ClaimedByAnother = hardwareStatusManager.IsClaimedByAnother.CurrentValue;
        }

        if (mustBeClaimed && sink != null && sink.ClaimedByAnother)
        {
            throw new PosControlException("Device is claimed by another application.", ErrorCode.Claimed);
        }

        // 3. NotClaimed Check (ErrorCode.NotClaimed)
        if (mustBeClaimed && sink != null && !sink.Claimed)
        {
            throw new PosControlException("Device is not claimed.", ErrorCode.NotClaimed);
        }

        // 4. Disabled Check (ErrorCode.Disabled)
        if (mustBeEnabled && sink != null && !sink.DeviceEnabled)
        {
            throw new PosControlException("Device is disabled.", ErrorCode.Disabled);
        }

        // 5. Busy Check (ErrorCode.Busy)
        if (mustNotBeBusy && IsBusy)
        {
            throw new PosControlException("Device is busy.", ErrorCode.Busy);
        }
    }

    /// <inheritdoc/>
    public static void ThrowIfBusy(bool asyncProcessing)
    {
        if (asyncProcessing)
        {
            throw new PosControlException("Device is busy with an asynchronous operation.", ErrorCode.Busy);
        }
    }

    /// <inheritdoc/>
    public static void ThrowIfDepositInProgress(bool inProgress)
    {
        if (inProgress)
        {
            throw new PosControlException("Deposit in progress.", ErrorCode.Illegal);
        }
    }

    /// <inheritdoc/>
    public virtual void SetSuccess()
    {
        lock (stateLock)
        {
            ResultCode = (int)ErrorCode.Success;
            ResultCodeExtended = 0;
        }
    }

    /// <inheritdoc/>
    public virtual void SetFailure(ErrorCode code, int codeEx = 0)
    {
        lock (stateLock)
        {
            ResultCode = (int)code;
            ResultCodeExtended = codeEx;
        }
    }

    /// <inheritdoc/>
    public virtual void HandleFailure(DeviceErrorCode code, int codeEx = 0)
    {
        // [FIX] Extended Code が未指定(0)の場合、DeviceErrorCode に応じたデフォルト値をセットする。
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

    /// <inheritdoc/>
    public void FireEvent(EventArgs e)
    {
        if (e is DataEventArgs de)
        {
            EventSink?.QueueDataEvent(de);
        }
        else if (e is StatusUpdateEventArgs se)
        {
            EventSink?.QueueStatusUpdateEvent(se);
        }
        else
        {
            EventSink?.QueueEvent(e);
        }
    }

    /// <inheritdoc/>
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
            logger?.LogError(ex, "Unexpected error during command execution.");

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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
