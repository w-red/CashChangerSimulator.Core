using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Microsoft.PointOfService.BasicServiceObjects;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device;

/// <summary>UPOS の CashChanger サービスオブジェクトをシミュレートするクラス。</summary>
[ServiceObject(DeviceType.CashChanger, "SimulatorCashChanger", "Virtual Cash Changer Simulator", 1, 14)]
public class SimulatorCashChanger : CashChangerBasic
{
    private readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly CashChangerManager _manager;
    private readonly OverallStatusAggregator _statusAggregator;
    private readonly IDisposable _statusSubscription;
    private readonly SimulatorConfiguration _config;

    private readonly DepositController _depositController;
    private readonly DispenseController _dispenseController;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly ILogger<SimulatorCashChanger> _logger;

    // Status tracking for StatusUpdateEvent transitions
    private CashChangerStatus _lastCashChangerStatus = CashChangerStatus.OK;
    private CashChangerFullStatus _lastFullStatus = CashChangerFullStatus.OK;

    // Async processing state
    private bool _asyncProcessing;
    private int _asyncResultCode;
    private int _asyncResultCodeExtended;

    /// <summary>テスト用のイベント通知アクション。</summary>
    internal Action<EventArgs>? OnEventQueued; // For testing

    /// <summary>テスト用: VerifyState チェックをスキップする。OPOS ライフサイクルが利用できない単体テスト環境で使用。</summary>
    public bool SkipStateVerification { get; set; }

    /// <summary>デバイスの有効状態を取得または設定します。シミュレータでは常に成功するようにオーバーライドします。</summary>
    public override bool DeviceEnabled { get; set; }
    /// <summary>データイベント通知の有効状態を取得または設定します。シミュレータでは常に成功するようにオーバーライドします。</summary>
    public override bool DataEventEnabled { get; set; }

    /// <summary>入金データのリアルタイム通知が有効かどうかを取得または設定します。</summary>
    public override bool RealTimeDataEnabled { get; set; }

    /// <summary>デバイスがリアルタイムデータの通知能力を持っているかどうか。</summary>
    public override bool CapRealTimeData => true;

    /// <summary>イベントを通知し、必要に応じてキューに追加します。</summary>
    /// <param name="e">通知するイベント引数。</param>
    protected virtual void NotifyEvent(EventArgs e)
    {
        OnEventQueued?.Invoke(e);
        if (e is DataEventArgs de)
        {
            QueueEvent(de);
            
            // Phase 24.1: Record DataEvent in TransactionHistory for UI Activity Feed
            _history.Add(new TransactionEntry(
                DateTime.Now,
                TransactionType.DataEvent,
                0,
                new Dictionary<DenominationKey, int>()
            ));
        }
        else if (e is StatusUpdateEventArgs se)
        {
            QueueEvent(se);
        }
    }

    // ========== Simulator UI Helpers for Lifecycle Verification ==========

    /// <summary>シミュレータUIからの Open 呼び出しを受け付けます。</summary>
    public new virtual void Open()
    {
        _logger.LogInformation(
            "OPOS Open called via simulator.");
    }

    /// <summary>シミュレータUIからの Close 呼び出しを受け付けます。</summary>
    public new virtual void Close()
    {
        _logger.LogInformation(
            "OPOS Close called via simulator.");
    }

    /// <summary>シミュレータUIからの Claim 呼び出しを受け付けます。</summary>
    public new virtual void Claim(int timeout)
    {
        _logger.LogInformation("OPOS Claim({0}) called via simulator.", timeout);
    }

    /// <summary>シミュレータUIからの Release 呼び出しを受け付けます。</summary>
    public new virtual void Release()
    {
        _logger.LogInformation(
            "OPOS Release called via simulator.");
    }

    /// <summary>サービスプロバイダーを使用して SimulatorCashChanger の新しいインスタンスを初期化します（DI用）。</summary>
    public SimulatorCashChanger()
        : this(
            SimulatorServices.TryResolve<ConfigurationProvider>(),
            SimulatorServices.TryResolve<Inventory>(),
            SimulatorServices.TryResolve<TransactionHistory>(),
            SimulatorServices.TryResolve<CashChangerManager>(),
            SimulatorServices.TryResolve<DepositController>(),
            SimulatorServices.TryResolve<DispenseController>(),
            SimulatorServices.TryResolve<OverallStatusAggregatorProvider>(),
            SimulatorServices.TryResolve<HardwareStatusManager>())
    {
    }

    /// <summary>SimulatorCashChanger の新しいインスタンスを初期化します。</summary>
    public SimulatorCashChanger(
        ConfigurationProvider? configProvider,
        Inventory? inventory,
        TransactionHistory? history,
        CashChangerManager? manager,
        DepositController? depositController,
        DispenseController? dispenseController,
        OverallStatusAggregatorProvider? aggregatorProvider,
        HardwareStatusManager? hardwareStatusManager)
    {
        // Load settings from TOML
        _config = configProvider?.Config ?? new SimulatorConfiguration();

        DevicePath = "SimulatorCashChanger";
        _hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();

        _logger = LogProvider.CreateLogger<SimulatorCashChanger>();
        _logger.ZLogInformation($"SimulatorCashChanger initialized.");

        _inventory = inventory ?? new Inventory();
        _history = history ?? new TransactionHistory();
        _manager = manager ?? new CashChangerManager(_inventory, _history, new ChangeCalculator());
        _depositController = depositController ?? new DepositController(_inventory, _hardwareStatusManager);
        _dispenseController = dispenseController ?? new DispenseController(_manager, _hardwareStatusManager, new HardwareSimulator(configProvider));

        // Status monitors / Aggregator
        var monitors = _inventory
            .AllCounts
            .Select(kv => (kv.Key, _config.GetDenominationSetting(kv.Key)))
            .Select(x =>
                new CashStatusMonitor(
                    _inventory,
                    x.Key,
                    x.Item2.NearEmpty,
                    x.Item2.NearFull,
                    x.Item2.Full))
            .ToList();
        _statusAggregator =
            aggregatorProvider
            ?.Aggregator
            ?? new OverallStatusAggregator(monitors);

        // Active currency initialization
        _activeCurrencyCode =
            _config
            .Inventory
            .Keys
            .FirstOrDefault()
            ?? "JPY";

        // Subscribe to status changes for StatusUpdateEvent
        _statusSubscription = Disposable.Combine(
            _statusAggregator.DeviceStatus.Subscribe(status =>
            {
                var newDeviceStatus = status switch
                {
                    CashStatus.Empty => CashChangerStatus.Empty,
                    CashStatus.NearEmpty => CashChangerStatus.NearEmpty,
                    _ => CashChangerStatus.OK
                };

                if (newDeviceStatus != _lastCashChangerStatus)
                {
                    var previousStatus = _lastCashChangerStatus;
                    _lastCashChangerStatus = newDeviceStatus;

                    if (newDeviceStatus == CashChangerStatus.OK &&
                        previousStatus is CashChangerStatus.Empty or CashChangerStatus.NearEmpty)
                    {
                        NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.EmptyOk));
                    }
                    else
                    {
                        var code = newDeviceStatus switch
                        {
                            CashChangerStatus.Empty => (int)UposCashChangerStatusUpdateCode.Empty,
                            CashChangerStatus.NearEmpty => (int)UposCashChangerStatusUpdateCode.NearEmpty,
                            _ => (int)UposCashChangerStatusUpdateCode.Ok
                        };
                        NotifyEvent(new StatusUpdateEventArgs(code));
                    }
                }
            }),
            _statusAggregator.FullStatus.Subscribe(status =>
            {
                var newFullStatus = status switch
                {
                    CashStatus.Full => CashChangerFullStatus.Full,
                    CashStatus.NearFull => CashChangerFullStatus.NearFull,
                    _ => CashChangerFullStatus.OK
                };

                if (newFullStatus != _lastFullStatus)
                {
                    var previousFullStatus = _lastFullStatus;
                    _lastFullStatus = newFullStatus;

                    if (newFullStatus == CashChangerFullStatus.OK &&
                        previousFullStatus is CashChangerFullStatus.Full or CashChangerFullStatus.NearFull)
                    {
                        NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.FullOk));
                    }
                    else
                    {
                        NotifyEvent(new StatusUpdateEventArgs((int)newFullStatus));
                    }
                }
            }),
            _hardwareStatusManager.IsJammed.Subscribe(jammed =>
            {
                if (jammed)
                {
                    NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Jam));
                }
                else
                {
                    _lastCashChangerStatus = CashChangerStatus.OK;
                    NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Ok));
                }
            }),
            _hardwareStatusManager.IsOverlapped.Subscribe(overlapped =>
            {
                if (overlapped)
                {
                    _logger.ZLogWarning($"Device reported OVERLAP error.");
                }
            }),
            _depositController.Changed.Subscribe(_ =>
            {
                if (_depositController.DepositStatus == CashDepositStatus.Count && !_depositController.IsPaused && DataEventEnabled)
                {
                    // DataEvent is fired if RealTimeDataEnabled is true OR if the deposit has been fixed (FixDeposit called)
                    if (RealTimeDataEnabled || _depositController.IsFixed)
                    {
                        NotifyEvent(new DataEventArgs(0));
                    }
                }
            }),
            _dispenseController.Changed.Subscribe(_ =>
            {
                // Map Busy state to UPOS State
                _asyncProcessing = _dispenseController.IsBusy;
            })
        );
    }

    private string _activeCurrencyCode;

    /// <summary>デバイスの健康状態を確認します。</summary>
    public override string CheckHealth(HealthCheckLevel level) => "OK";
    /// <summary>健康状態のテキスト表現を取得します。</summary>
    public override string CheckHealthText => "OK";

    // ========== Deposit Methods (UPOS v1.5+) ==========

    /// <summary>現金投入処理を開始します。</summary>
    public override void BeginDeposit()
    {
        VerifyState();
        ThrowIfBusy();
        _depositController.BeginDeposit();
    }

    /// <summary>現金投入処理を終了します。</summary>
    public override void EndDeposit(CashDepositAction action)
    {
        VerifyState();
        _depositController.EndDeposit(action);
    }

    /// <summary>投入された現金の計数を確定します。</summary>
    public override void FixDeposit()
    {
        VerifyState();
        _depositController.FixDeposit();

        if (DataEventEnabled && CapDepositDataEvent)
        {
            QueueEvent(new DataEventArgs(0));
        }
    }

    /// <summary>現金投入処理を一時停止または再開します。</summary>
    public override void PauseDeposit(CashDepositPause control)
    {
        VerifyState();
        _depositController.PauseDeposit(control);
    }

    // ========== Dispense Methods ==========

    private void ThrowIfDepositInProgress()
    {
        if (_depositController.IsDepositInProgress)
        {
            throw new PosControlException(
                "Cash cannot be dispensed because cash acceptance is in progress.",
                ErrorCode.Illegal);
        }
    }

    private void ThrowIfBusy()
    {
        if (_asyncProcessing)
        {
            throw new PosControlException("Device is busy with an asynchronous operation.", ErrorCode.Busy);
        }
    }

    /// <summary>UPOS ライフサイクルの状態を検証します。Open/Claim/Enable が行われていない場合は例外をスローします。</summary>
    private void VerifyState()
    {
        if (SkipStateVerification) return;

        if (State == ControlState.Closed)
        {
            throw new PosControlException("Device is not open.", ErrorCode.Closed);
        }
        if (State == ControlState.Idle || State == ControlState.Error)
        {
            // Idle/Error means Open but not Claimed or not Enabled
            // For simplicity, we allow operations if State is Idle or Error (means at least Open)
        }
    }

    /// <summary>指定された金額の釣銭を払い出します。</summary>
    public override void DispenseChange(int amount)
    {
        VerifyState();
        if (amount <= 0)
        {
            throw new PosControlException("Amount must be positive", ErrorCode.Illegal);
        }

        ThrowIfDepositInProgress();
        ThrowIfBusy();

        var factor = GetCurrencyFactor();
        var decimalAmount = amount / factor;

        void OnComplete(ErrorCode code, int codeEx)
        {
            if (code == ErrorCode.Success)
            {
                _logger.ZLogInformation($"DispenseChange completed successfully.");
            }
            else
            {
                _logger.ZLogError($"DispenseChange failed: {code}");
            }

            if (AsyncMode)
            {
                _asyncResultCode = (int)code;
                _asyncResultCodeExtended = codeEx;
                _asyncProcessing = false; // Set this BEFORE NotifyEvent
                NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.AsyncFinished));
            }
        }

        var task = _dispenseController.DispenseChangeAsync(decimalAmount, AsyncMode, OnComplete, CurrencyCode);
        if (!AsyncMode)
        {
            task.GetAwaiter().GetResult();
        }
    }

    /// <summary>指定された金種と枚数の現金を払い出します。</summary>
    public override void DispenseCash(CashCount[] cashCounts)
    {
        VerifyState();
        ThrowIfDepositInProgress();
        ThrowIfBusy();

        var dict = new Dictionary<DenominationKey, int>();
        try
        {
            foreach (var cc in cashCounts)
            {
                if (cc.Count < 0)
                {
                    throw new PosControlException("Count cannot be negative.", ErrorCode.Illegal);
                }

                var cashType = (cc.Type == CashCountType.Bill)
                    ? CashType.Bill
                    : CashType.Coin;

                var factor = GetCurrencyFactor(_activeCurrencyCode);
                var val = cc.NominalValue / factor;
                var key = new DenominationKey(val, cashType, _activeCurrencyCode);

                // Check if this denomination exists in the current currency's inventory
                if (!_inventory.AllCounts.Any(kv => kv.Key == key))
                {
                    throw new PosControlException($"Denomination {key} is not registered for the current currency ({_activeCurrencyCode}).", ErrorCode.Illegal);
                }

                if (_inventory.GetCount(key) < cc.Count)
                {
                    throw new InsufficientCashException($"Insufficient inventory for {key}. Required: {cc.Count}, Available: {_inventory.GetCount(key)}");
                }

                dict[key] = cc.Count;
            }
        }
        catch (InsufficientCashException ex)
        {
            throw new PosControlException(ex.Message, ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.OverDispense, ex);
        }

        void OnComplete(ErrorCode code, int codeEx)
        {
            if (AsyncMode)
            {
                _asyncResultCode = (int)code;
                _asyncResultCodeExtended = codeEx;
                _asyncProcessing = false;
                NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.AsyncFinished));
            }
        }

        var task = _dispenseController.DispenseCashAsync(dict, AsyncMode, OnComplete);
        if (!AsyncMode)
        {
            task.GetAwaiter().GetResult();
        }
    }

    // ========== AdjustCashCounts ==========
    
    /// <summary>現在の現金在庫数を手動で調整（上書き）します。</summary>
    public override void AdjustCashCounts(IEnumerable<CashCount> cashCounts)
    {
        VerifyState();
        ThrowIfBusy();

        foreach (var cc in cashCounts)
        {
            if (cc.Count < 0)
            {
                throw new PosControlException("Count cannot be negative.", ErrorCode.Illegal);
            }

            var cashType = (cc.Type == CashCountType.Bill)
                ? CashType.Bill
                : CashType.Coin;

            var factor = GetCurrencyFactor(_activeCurrencyCode);
            var val = cc.NominalValue / factor;
            var key = new DenominationKey(val, cashType, _activeCurrencyCode);
            
            _inventory.SetCount(key, cc.Count);
        }
        _logger.ZLogInformation($"AdjustCashCounts completed. Updated {cashCounts.Count()} denominations.");
    }

    // ========== ReadCashCounts ==========

    /// <summary>現在の現金在庫数を読み取ります。</summary>
    public override CashCounts ReadCashCounts()
    {
        VerifyState();
        ThrowIfBusy();
        var sorted = _inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .OrderBy(kv => kv.Key.Type) // Coin(0) before Bill(1)
            .ThenBy(kv => kv.Key.Value);

        var list = sorted.Select(kv => new CashCount(
            (kv.Key.Type == CashType.Bill) ? CashCountType.Bill : CashCountType.Coin,
            GetNominalValue(kv.Key),
            kv.Value)).ToList();

        return new CashCounts([.. list], _inventory.HasDiscrepancy);
    }

    /// <summary>ベンダー固有のコマンドをデバイスに送信します。</summary>
    public override DirectIOData DirectIO(int command, int data, object obj)
    {
        switch (command)
        {
            case DirectIOCommands.SetOverlap:
                _hardwareStatusManager.SetOverlapped(data != 0);
                _logger.ZLogInformation($"DirectIO: SET_OVERLAP to {data != 0}");
                return new DirectIOData(data, obj);

            case DirectIOCommands.SetJam:
                _hardwareStatusManager.SetJammed(data != 0);
                _logger.ZLogInformation($"DirectIO: SET_JAM to {data != 0}");
                return new DirectIOData(data, obj);

            case DirectIOCommands.GetVersion:
                var version = $"SimulatorCashChanger v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}";
                _logger.ZLogInformation($"DirectIO: GET_VERSION -> {version}");
                return new DirectIOData(data, version);

            case DirectIOCommands.GetDepositedSerials:
                var serials = string.Join(",", _depositController.LastDepositedSerials);
                _logger.ZLogInformation($"DirectIO: GET_DEPOSITED_SERIALS -> {serials}");
                return new DirectIOData(data, serials);

            case DirectIOCommands.SimulateRemoved:
                NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Removed));
                return new DirectIOData(data, "REMOVED");

            case DirectIOCommands.SimulateInserted:
                NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Inserted));
                return new DirectIOData(data, "INSERTED");

            case DirectIOCommands.SetDiscrepancy:
                _inventory.HasDiscrepancy = (data != 0);
                _logger.ZLogInformation($"DirectIO: SET_DISCREPANCY to {_inventory.HasDiscrepancy}");
                return new DirectIOData(data, obj);

            case DirectIOCommands.AdjustCashCountsStr:
                if (obj is string strVal)
                {
                    try
                    {
                        var activeKeys = _inventory.AllCounts.Select(kv => kv.Key).ToList();
                        var factor = GetCurrencyFactor(_activeCurrencyCode);
                        var counts = CashCountParser.Parse(strVal, activeKeys, factor);
                        AdjustCashCounts(counts); // Calls our core adjust logic
                        _logger.ZLogInformation($"DirectIO: ADJUST_CASH_COUNTS_STR completed for '{strVal}'");
                    }
                    catch (Exception ex)
                    {
                        _logger.ZLogError($"DirectIO: ADJUST_CASH_COUNTS_STR failed: {ex.Message}");
                        throw new PosControlException($"Failed to parse adjust string: {ex.Message}", ErrorCode.Illegal, ex);
                    }
                }
                else
                {
                    throw new PosControlException("ADJUST_CASH_COUNTS_STR requires a string object.", ErrorCode.Illegal);
                }
                return new DirectIOData(data, obj);

            default:
                return new DirectIOData(data, obj);
        }
    }

    // ========== Status Properties ==========

    /// <summary>デバイスの現在の状態（正常、空、ニアエンプティなど）を取得します。</summary>
    public override CashChangerStatus DeviceStatus => _lastCashChangerStatus;
    /// <summary>デバイスの現在の満杯状態（正常、満杯、ニアフルなど）を取得します。</summary>
    public override CashChangerFullStatus FullStatus => _lastFullStatus;

    // ========== Async Properties ==========

    /// <summary>非同期モードで動作するかどうかを取得または設定します。</summary>
    public override bool AsyncMode { get; set; }
    /// <summary>最後の非同期操作の結果コードを取得します。</summary>
    public override int AsyncResultCode => _asyncResultCode;
    /// <summary>最後の非同期操作の拡張結果コードを取得します。</summary>
    public override int AsyncResultCodeExtended => _asyncResultCodeExtended;

    // ========== Currency Properties ==========

    /// <summary>現在アクティブな通貨コードを取得または設定します。</summary>
    public override string CurrencyCode
    {
        get => _activeCurrencyCode;
        set
        {
            if (CurrencyCodeList.Contains(value))
            {
                _activeCurrencyCode = value;
            }
            else
            {
                throw new PosControlException($"Unsupported currency: {value}", ErrorCode.Illegal);
            }
        }
    }
    /// <summary>サポートされている通貨コードの一覧を取得します。</summary>
    public override string[] CurrencyCodeList => [.. _config.Inventory.Keys.OrderBy(c => c)];
    /// <summary>入金可能な通貨コードの一覧を取得します。</summary>
    public override string[] DepositCodeList => CurrencyCodeList;

    /// <summary>アクティブな通貨の現在の現金一覧を取得します。</summary>
    public override CashUnits CurrencyCashList => BuildCashUnits();
    /// <summary>入金可能な現金の一覧を取得します。</summary>
    public override CashUnits DepositCashList => CapDeposit ? BuildCashUnits() : new CashUnits();
    /// <summary>払出可能な現金の一覧を取得します。</summary>
    public override CashUnits ExitCashList => BuildCashUnits();

    // ========== Deposit Properties ==========

    /// <summary>現在投入されている現金の合計金額を取得します。</summary>
    public override int DepositAmount => (int)Math.Round(_depositController.DepositAmount * GetCurrencyFactor());
    /// <summary>現在投入されている現金の金種別枚数を取得します。</summary>
    public override CashCount[] DepositCounts
    {
        get => [.. _depositController.DepositCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .Select(kv => new CashCount(
                kv.Key.Type == CashType.Bill ? CashCountType.Bill : CashCountType.Coin,
                GetNominalValue(kv.Key),
                kv.Value))];
    }
    /// <summary>現在の入金処理の状態を取得します。</summary>
    public override CashDepositStatus DepositStatus => _depositController.DepositStatus;

    // ========== Exit Properties ==========

    /// <summary>現在の排出口インデックスを取得または設定します。</summary>
    public override int CurrentExit { get => 1; set { } }
    /// <summary>デバイスの排出口の総数を取得します。</summary>
    public override int DeviceExits => 1;

    // ========== Capability Properties ==========

    /// <summary>入金機能があるかどうかを取得します。</summary>
    public override bool CapDeposit => true;
    /// <summary>入金データイベントをサポートしているかどうかを取得します。</summary>
    public override bool CapDepositDataEvent => true;
    /// <summary>入金の一時停止をサポートしているかどうかを取得します。</summary>
    public override bool CapPauseDeposit => true;
    /// <summary>入金の返却（払い戻し）をサポートしているかどうかを取得します。</summary>
    public override bool CapRepayDeposit => true;

    /// <summary>不一致の検出をサポートしているかどうかを取得します。</summary>
    public override bool CapDiscrepancy => false;
    /// <summary>満杯センサーをサポートしているかどうかを取得します。</summary>
    public override bool CapFullSensor => true;
    /// <summary>ニアフルセンサーをサポートしているかどうかを取得します。</summary>
    public override bool CapNearFullSensor => true;
    /// <summary>ニアエンプティセンサーをサポートしているかどうかを取得します。</summary>
    public override bool CapNearEmptySensor => true;
    /// <summary>空センサーをサポートしているかどうかを取得します。</summary>
    public override bool CapEmptySensor => true;

    // ========== Private Helpers ==========

    private CashUnits BuildCashUnits()
    {
        var activeUnits = _inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .OrderBy(kv => kv.Key.Value)
            .ToList();

        var coins = activeUnits
            .Where(kv => kv.Key.Type == CashType.Coin)
            .Select(kv => GetNominalValue(kv.Key))
            .ToArray();

        var bills = activeUnits
            .Where(kv => kv.Key.Type == CashType.Bill)
            .Select(kv => GetNominalValue(kv.Key))
            .ToArray();

        return new CashUnits(coins, bills);
    }

    private int GetNominalValue(DenominationKey key)
    {
        return (int)Math.Round(key.Value * GetCurrencyFactor(key.CurrencyCode));
    }

    private decimal GetCurrencyFactor(string? currencyCode = null)
    {
        var code = currencyCode ?? _activeCurrencyCode;
        return code switch
        {
            "USD" or "EUR" or "GBP" or "CAD" or "AUD" => 100m,
            _ => 1m
        };
    }
}
