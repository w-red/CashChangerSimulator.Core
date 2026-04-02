using CashChangerSimulator.Device;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>出金（払出）シーケンスを管理するコントローラー（仮想デバイス実装）。</summary>
/// <remarks>
/// UPOS などのプラットフォーム固有の SDK に依存せず、純粋な C# ロジックとして出金プロセスをシミュレートします。
/// </remarks>
public class DispenseController : IDisposable
{
    private readonly CashChangerManager _manager;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly IDeviceSimulator _simulator;
    private CancellationTokenSource? _dispenseCts;
    private readonly ILogger<DispenseController> _logger = LogProvider.CreateLogger<DispenseController>();
    private readonly Subject<Unit> _changed = new();
    private readonly CompositeDisposable _disposables = [];

    private readonly object _stateLock = new();
    private CashDispenseStatus _status = CashDispenseStatus.Idle;
    private bool _disposed;

    public DispenseController(
        CashChangerManager manager,
        HardwareStatusManager? hardwareStatusManager = null,
        IDeviceSimulator? simulator = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();
        _simulator = simulator ?? new HardwareSimulator(new ConfigurationProvider());
    }

    public virtual Observable<Unit> Changed => _changed;
    public virtual CashDispenseStatus Status { get { lock (_stateLock) return _status; } }
    public virtual bool IsBusy { get { lock (_stateLock) return _status == CashDispenseStatus.Busy; } }

    public virtual async Task DispenseChangeAsync(int amount, bool asyncMode, Action<DeviceErrorCode, int> onComplete, string? currencyCode = null)
    {
        lock (_stateLock)
        {
            if (_status == CashDispenseStatus.Busy) throw new DeviceException("Device is busy", DeviceErrorCode.Busy);
            if (!_hardwareStatusManager.IsConnected.Value) throw new DeviceException("Device not connected", DeviceErrorCode.Closed);
            if (_hardwareStatusManager.IsJammed.Value) throw new DeviceException("Device jammed", DeviceErrorCode.Failure);
            
            // [FIX] Return Failure for Overlapped to match expectations in DispenseControllerTests.
            // [修正] DispenseControllerTests の期待値に合わせて、オーバーラップ時は Failure を返します。
            if (_hardwareStatusManager.IsOverlapped.Value) throw new DeviceException("Device overlapped", DeviceErrorCode.Failure);

            _status = CashDispenseStatus.Busy;
        }
        _changed.OnNext(Unit.Default);
        await Task.Yield();

        _dispenseCts?.Cancel();
        _dispenseCts?.Dispose();
        _dispenseCts = new CancellationTokenSource();
        var token = _dispenseCts.Token;

        if (asyncMode)
        {
            _ = Task.Run(async () =>
            {
                try { await ExecuteDispense(() => _manager.Dispense(amount, currencyCode), onComplete, token); }
                catch (Exception ex) { _logger.ZLogError($"Background dispense error: {ex.Message}"); }
            }, token);
        }
        else
        {
            await ExecuteDispense(() => _manager.Dispense(amount, currencyCode), onComplete, token);
        }
    }

    public virtual async Task DispenseCashAsync(IReadOnlyDictionary<DenominationKey, int> counts, bool asyncMode, Action<DeviceErrorCode, int> onComplete)
    {
        lock (_stateLock)
        {
            if (_status == CashDispenseStatus.Busy) throw new DeviceException("Device is busy", DeviceErrorCode.Busy);
            if (!_hardwareStatusManager.IsConnected.Value) throw new DeviceException("Device not connected", DeviceErrorCode.Closed);
            if (_hardwareStatusManager.IsJammed.Value) throw new DeviceException("Device jammed", DeviceErrorCode.Failure);
            
            // [FIX] Return Failure for Overlapped to match expectations in DispenseControllerTests.
            // [修正] DispenseControllerTests の期待値に合わせて、オーバーラップ時は Failure を返します。
            if (_hardwareStatusManager.IsOverlapped.Value) throw new DeviceException("Device overlapped", DeviceErrorCode.Failure);

            _status = CashDispenseStatus.Busy;
        }
        _changed.OnNext(Unit.Default);
        await Task.Yield();

        _dispenseCts?.Cancel();
        _dispenseCts?.Dispose();
        _dispenseCts = new CancellationTokenSource();
        var token = _dispenseCts.Token;

        if (asyncMode)
        {
            _ = Task.Run(async () =>
            {
                try { await ExecuteDispense(() => _manager.Dispense(counts), onComplete, token); }
                catch (Exception ex) { _logger.ZLogError($"Background dispense error: {ex.Message}"); }
            }, token);
        }
        else
        {
            await ExecuteDispense(() => _manager.Dispense(counts), onComplete, token);
        }
    }

    private async Task ExecuteDispense(Action action, Action<DeviceErrorCode, int> onComplete, CancellationToken token)
    {
        try
        {
            await _simulator.SimulateDispenseAsync(token);
            token.ThrowIfCancellationRequested();
            action();
            lock (_stateLock)
            {
                _status = CashDispenseStatus.Idle;
            }
            onComplete(0, 0); // Success
        }
        catch (OperationCanceledException)
        {
            lock (_stateLock)
            {
                _status = CashDispenseStatus.Idle;
            }
        }
        catch (InsufficientCashException ex)
        {
            // [FIX] Error 時にもステータスをリセットしないと、デバイスが Busy のまま固まってしまう。
            lock (_stateLock)
            {
                _status = CashDispenseStatus.Error;
            }
            // Throw DeviceException with Extended (201) for the Mediator/Tests to catch
            onComplete(DeviceErrorCode.Extended, 201);
            throw new DeviceException(ex.Message, DeviceErrorCode.Extended, 201);
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                _status = CashDispenseStatus.Error;
            }
            
            DeviceErrorCode code = DeviceErrorCode.Failure;
            int codeEx = 0;

            if (ex is DeviceException dex)
            {
                code = dex.ErrorCode;
                codeEx = dex.ErrorCodeExtended;
            }
            else
            {
                // Attempt to extract error codes from external exceptions (e.g. PosControlException in tests)
                // without adding a direct dependency on POS.NET in this virtual layer.
                var type = ex.GetType();
                var pCode = type.GetProperty("ErrorCode");
                var pCodeEx = type.GetProperty("ErrorCodeExtended");

                if (pCode != null)
                {
                    var val = pCode.GetValue(ex);
                    if (val != null) code = (DeviceErrorCode)Convert.ToInt32(val);
                }
                if (pCodeEx != null)
                {
                    var valEx = pCodeEx.GetValue(ex);
                    if (valEx != null) codeEx = Convert.ToInt32(valEx);
                }
            }

            onComplete(code, codeEx);
            throw new DeviceException($"Dispense failed: {ex.Message} (Type: {ex.GetType().Name})", code, codeEx);
        }
        finally
        {
            _changed.OnNext(Unit.Default);
        }
    }

    public virtual void ClearOutput()
    {
        _dispenseCts?.Cancel();
        lock (_stateLock)
        {
            if (_status != CashDispenseStatus.Idle)
            {
                _status = CashDispenseStatus.Idle;
                _changed.OnNext(Unit.Default);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _dispenseCts?.Cancel();
        _dispenseCts?.Dispose();
        _disposables.Dispose();
        _changed.OnCompleted();
        _simulator.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
