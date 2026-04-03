using System.Threading;
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

    private readonly Lock _stateLock = new();
    private CashDispenseStatus _status = CashDispenseStatus.Idle;
    private DeviceErrorCode _lastErrorCode = DeviceErrorCode.Success;
    private int _lastErrorCodeExtended = 0;
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
    public virtual DeviceErrorCode LastErrorCode { get { lock (_stateLock) return _lastErrorCode; } }
    public virtual int LastErrorCodeExtended { get { lock (_stateLock) return _lastErrorCodeExtended; } }

    public virtual async Task DispenseChangeAsync(int amount, bool asyncMode, string? currencyCode = null)
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
            _lastErrorCode = DeviceErrorCode.Success;
            _lastErrorCodeExtended = 0;
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
                try { await ExecuteDispense(() => _manager.Dispense(amount, currencyCode), token); }
                catch (Exception ex) { _logger.ZLogError(ex, $"Background dispense error: {ex.Message}"); }
            }, token);
        }
        else
        {
            await ExecuteDispense(() => _manager.Dispense(amount, currencyCode), token);
        }
    }

    public virtual async Task DispenseCashAsync(IReadOnlyDictionary<DenominationKey, int> counts, bool asyncMode)
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
            _lastErrorCode = DeviceErrorCode.Success;
            _lastErrorCodeExtended = 0;
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
                try { await ExecuteDispense(() => _manager.Dispense(counts), token); }
                catch (Exception ex) { _logger.ZLogError(ex, $"Background dispense error: {ex.Message}"); }
            }, token);
        }
        else
        {
            await ExecuteDispense(() => _manager.Dispense(counts), token);
        }
    }

    private async Task ExecuteDispense(Action action, CancellationToken token)
    {
        DeviceErrorCode code = DeviceErrorCode.Success;
        int codeEx = 0;
        bool isError = false;

        try
        {
            await _simulator.SimulateDispenseAsync(token);
            token.ThrowIfCancellationRequested();
            action();
        }
        catch (OperationCanceledException)
        {
            // Canceled: status back to Idle without specific error result.
        }
        catch (InsufficientCashException ex)
        {
            isError = true;
            code = DeviceErrorCode.Extended;
            codeEx = 201;
            _logger.ZLogError(ex, $"Insufficient cash: {ex.Message}");
        }
        catch (Exception ex)
        {
            isError = true;
            code = DeviceErrorCode.Failure;
            codeEx = 0;

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
            _logger.ZLogError(ex, $"Dispense failed: {ex.Message}");
        }
        finally
        {
            lock (_stateLock)
            {
                _status = isError ? CashDispenseStatus.Error : CashDispenseStatus.Idle;
                _lastErrorCode = code;
                _lastErrorCodeExtended = codeEx;
            }
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
                _lastErrorCode = DeviceErrorCode.Cancelled;
                _lastErrorCodeExtended = 0;
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
