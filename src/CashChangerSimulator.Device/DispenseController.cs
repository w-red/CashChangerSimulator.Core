using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device;

/// <summary>出金（払出）シーケンスを管理するコントローラー。 UPOS の DispenseCash / DispenseChange のライフサイクル（IDLE -> BUSY -> IDLE/ERROR）を制御します。</summary>
public class DispenseController(
    CashChangerManager manager,
    SimulationSettings? config = null,
    HardwareStatusManager? hardwareStatusManager = null)
    : IDisposable
{
    private readonly SimulationSettings _config = config ?? new SimulationSettings();
    private readonly HardwareStatusManager _hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();
    private readonly ILogger<DispenseController> _logger = Core.LogProvider.CreateLogger<DispenseController>();
    private readonly Subject<Unit> _changed = new();
    private readonly CompositeDisposable _disposables = [];

    private CashDispenseStatus _status = CashDispenseStatus.Idle;

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed => _changed;

    /// <summary>現在の出金ステータス。</summary>
    public CashDispenseStatus Status => _status;

    /// <summary>出金処理中かどうか（BUSY 状態）。</summary>
    public bool IsBusy => _status == CashDispenseStatus.Busy;

    /// <summary>指定された金額を払い出します（内訳自動計算）。</summary>
    public async Task DispenseChangeAsync(decimal amount, bool asyncMode, Action<ErrorCode, int> onComplete, string? currencyCode = null)
    {
        if (IsBusy) throw new PosControlException("Device is busy", ErrorCode.Busy);
        if (_hardwareStatusManager.IsJammed.Value) throw new PosControlException("Device is jammed", ErrorCode.Failure);

        _status = CashDispenseStatus.Busy;
        _changed.OnNext(Unit.Default);

        if (asyncMode)
        {
            _ = Task.Run(() => ExecuteDispense(() => manager.Dispense(amount, currencyCode), onComplete));
        }
        else
        {
            await ExecuteDispense(() => manager.Dispense(amount, currencyCode), onComplete);
        }
    }

    /// <summary>指定された金種・枚数の現金を払い出します。</summary>
    public async Task DispenseCashAsync(IReadOnlyDictionary<DenominationKey, int> counts, bool asyncMode, Action<ErrorCode, int> onComplete)
    {
        if (IsBusy) throw new PosControlException("Device is busy", ErrorCode.Busy);
        if (_hardwareStatusManager.IsJammed.Value) throw new PosControlException("Device is jammed", ErrorCode.Failure);

        _status = CashDispenseStatus.Busy;
        _changed.OnNext(Unit.Default);

        if (asyncMode)
        {
            _ = Task.Run(() => ExecuteDispense(() => manager.Dispense(counts), onComplete));
        }
        else
        {
            await ExecuteDispense(() => manager.Dispense(counts), onComplete);
        }
    }

    private async Task ExecuteDispense(Action action, Action<ErrorCode, int> onComplete)
    {
        try
        {
            _logger.ZLogInformation($"Dispense operation started.");

            await SimulationBehavior.ApplyDelayAsync(_config);
            SimulationBehavior.ThrowIfRandomError(_config);

            action();
            _status = CashDispenseStatus.Idle;
            _logger.ZLogInformation($"Dispense operation completed successfully.");
            onComplete(ErrorCode.Success, 0);
        }
        catch (PosControlException ex)
        {
            _logger.ZLogError(ex, $"Dispense operation failed: {ex.ErrorCode}");
            _status = CashDispenseStatus.Error;
            onComplete(ex.ErrorCode, ex.ErrorCodeExtended);
            throw;
        }
        catch (InsufficientCashException ex)
        {
            _logger.ZLogError(ex, $"Dispense operation failed due to shortage: {ex.Message}");
            _status = CashDispenseStatus.Error;
            onComplete(ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.OverDispense);
            throw new PosControlException(ex.Message, ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.OverDispense);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Dispense operation encountered an unexpected error.");
            _status = CashDispenseStatus.Error;
            onComplete(ErrorCode.Failure, 0);
            throw new PosControlException("Unexpected error during dispense", ErrorCode.Failure, 0, ex);
        }
        finally
        {
            _changed.OnNext(Unit.Default);
        }
    }

    /// <summary>エラー状態をクリアし、Idle に戻します。</summary>
    public void ClearError()
    {
        if (_status == CashDispenseStatus.Error)
        {
            _status = CashDispenseStatus.Idle;
            _changed.OnNext(Unit.Default);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        _changed.OnCompleted();
        GC.SuppressFinalize(this);
    }
}
