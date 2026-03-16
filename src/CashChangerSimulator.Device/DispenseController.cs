using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device;

/// <summary>出金（払出）シーケンスを管理するコントローラー。</summary>
/// <remarks>
/// 金額指定または金種指定による現金の払い出し処理を制御します。
/// <see cref="IDeviceSimulator"/> と連携し、非同期実行やキャンセルのロジックを提供します。
/// </remarks>
/// <param name="manager">在庫操作を統括する <see cref="CashChangerManager"/>。</param>
/// <param name="hardwareStatusManager">デバイスの状態を管理する <see cref="HardwareStatusManager"/>。未指定時は新規作成されます。</param>
/// <param name="simulator">ハードウェア動作を模擬する <see cref="IDeviceSimulator"/>。未指定時は <see cref="HardwareSimulator"/> が使用されます。</param>
public class DispenseController(
    CashChangerManager manager,
    HardwareStatusManager? hardwareStatusManager = null,
    IDeviceSimulator? simulator = null) : IDisposable
{
    private static T EnsureNotNull<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }

    private readonly CashChangerManager _manager = EnsureNotNull(manager);
    private readonly HardwareStatusManager _hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();
    private readonly IDeviceSimulator _simulator = simulator ?? new HardwareSimulator(new ConfigurationProvider());
    private CancellationTokenSource? _dispenseCts;
    private readonly ILogger<DispenseController> _logger = Core.LogProvider.CreateLogger<DispenseController>();
    private readonly Subject<Unit> _changed = new();
    private readonly CompositeDisposable _disposables = [];

    private CashDispenseStatus _status = CashDispenseStatus.Idle;

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public virtual Observable<Unit> Changed => _changed;

    /// <summary>現在の出金ステータス。</summary>
    public virtual CashDispenseStatus Status => _status;

    /// <summary>出金処理中かどうか（BUSY 状態）。</summary>
    public virtual bool IsBusy => _status == CashDispenseStatus.Busy;

    /// <summary>指定された金額を払い出します（内訳自動計算）。</summary>
    public virtual async Task DispenseChangeAsync(decimal amount, bool asyncMode, Action<ErrorCode, int> onComplete, string? currencyCode = null)
    {
        ArgumentNullException.ThrowIfNull(onComplete);
        if (IsBusy) throw new PosControlException("Device is busy", ErrorCode.Busy);

        if (!_hardwareStatusManager.IsConnected.Value)
        {
            throw new PosControlException("Device is not open (Closed).", ErrorCode.Closed);
        }

        if (_hardwareStatusManager.IsJammed.Value || _hardwareStatusManager.IsOverlapped.Value)
        {
            throw new PosControlException("Device is in error state. Cannot dispense.", ErrorCode.Failure);
        }

        _status = CashDispenseStatus.Busy;
        _changed.OnNext(Unit.Default);
        
        _dispenseCts?.Cancel();
        _dispenseCts?.Dispose();
        _dispenseCts = new CancellationTokenSource();
        var token = _dispenseCts.Token;

        if (asyncMode)
        {
            _ = Task.Run(() => ExecuteDispense(() => _manager.Dispense(amount, currencyCode), onComplete, token), token);
        }
        else
        {
            await ExecuteDispense(() => _manager.Dispense(amount, currencyCode), onComplete, token);
        }
    }

    /// <summary>指定された金種・枚数の現金を払い出します。</summary>
    public virtual async Task DispenseCashAsync(IReadOnlyDictionary<DenominationKey, int> counts, bool asyncMode, Action<ErrorCode, int> onComplete)
    {
        ArgumentNullException.ThrowIfNull(counts);
        ArgumentNullException.ThrowIfNull(onComplete);

        if (IsBusy) throw new PosControlException("Device is busy", ErrorCode.Busy);

        if (!_hardwareStatusManager.IsConnected.Value)
        {
            throw new PosControlException("Device is not open (Closed).", ErrorCode.Closed);
        }

        if (_hardwareStatusManager.IsJammed.Value || _hardwareStatusManager.IsOverlapped.Value)
        {
            throw new PosControlException("Device is in error state. Cannot dispense.", ErrorCode.Failure);
        }

        _status = CashDispenseStatus.Busy;
        _changed.OnNext(Unit.Default);
        
        _dispenseCts?.Cancel();
        _dispenseCts?.Dispose();
        _dispenseCts = new CancellationTokenSource();
        var token = _dispenseCts.Token;

        if (asyncMode)
        {
            _ = Task.Run(() => ExecuteDispense(() => _manager.Dispense(counts), onComplete, token), token);
        }
        else
        {
            await ExecuteDispense(() => _manager.Dispense(counts), onComplete, token);
        }
    }

    private async Task ExecuteDispense(Action action, Action<ErrorCode, int> onComplete, CancellationToken token)
    {
        try
        {
            _logger.ZLogInformation($"Dispense operation started.");
 
            await SimulateHardwareAsync(token);
 
            token.ThrowIfCancellationRequested();

            action();
            _status = CashDispenseStatus.Idle;
            _logger.ZLogInformation($"Dispense operation completed successfully.");
            onComplete(ErrorCode.Success, 0);
        }
        catch (OperationCanceledException)
        {
            _logger.ZLogInformation($"Dispense operation was canceled.");
            _status = CashDispenseStatus.Idle;
        }
        catch (PosControlException ex)
        {
            _logger.ZLogError(ex, $"Dispense operation failed: {ex.ErrorCode}");
            _hardwareStatusManager.SetDeviceError((int)ex.ErrorCode, ex.ErrorCodeExtended);
            _status = CashDispenseStatus.Error;
            onComplete(ex.ErrorCode, ex.ErrorCodeExtended);
            throw;
        }
        catch (InsufficientCashException ex)
        {
            _logger.ZLogError(ex, $"Dispense operation failed due to shortage: {ex.Message}");
            _hardwareStatusManager.SetDeviceError((int)ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.OverDispense);
            _status = CashDispenseStatus.Error;
            onComplete(ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.OverDispense);
            throw new PosControlException(ex.Message, ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.OverDispense);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Dispense operation encountered an unexpected error.");
            _hardwareStatusManager.SetDeviceError((int)ErrorCode.Failure, 0);
            _status = CashDispenseStatus.Error;
            onComplete(ErrorCode.Failure, 0);
            throw new PosControlException("Unexpected error during dispense", ErrorCode.Failure, 0, ex);
        }
        finally
        {
            _changed.OnNext(Unit.Default);
        }
    }

    private async Task SimulateHardwareAsync(CancellationToken token)
    {
        // ビジネスロジックから物理デバイス待機等のシミュレーションを切り離す
        await _simulator.SimulateDispenseAsync(token);
    }
    
    /// <summary>保留中の出金操作をすべてキャンセルします。</summary>
    public virtual void ClearOutput()
    {
        _dispenseCts?.Cancel();
        _dispenseCts?.Dispose();
        _dispenseCts = null;
        if (_status == CashDispenseStatus.Busy)
        {
            _status = CashDispenseStatus.Idle;
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
        _dispenseCts?.Dispose();
        _disposables.Dispose();
        _changed.OnCompleted();
        GC.SuppressFinalize(this);
    }
}
