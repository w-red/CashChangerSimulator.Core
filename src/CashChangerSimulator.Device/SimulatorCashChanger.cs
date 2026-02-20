using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
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

    /// <summary>イベントを通知し、必要に応じてキューに追加します。</summary>
    /// <param name="e">通知するイベント引数。</param>
    protected virtual void NotifyEvent(EventArgs e)
    {
        OnEventQueued?.Invoke(e);
        if (e is DataEventArgs de)
        {
            QueueEvent(de);
        }
        else if (e is StatusUpdateEventArgs se)
        {
            QueueEvent(se);
        }
    }

    /// <summary>SimulatorCashChanger の新しいインスタンスを初期化します。</summary>
    public SimulatorCashChanger() : this(null, null, null, null, null, null, null, null) { }

    internal SimulatorCashChanger(
        SimulatorConfiguration? config = null,
        Inventory? inventory = null,
        TransactionHistory? history = null,
        CashChangerManager? manager = null,
        DepositController? depositController = null,
        DispenseController? dispenseController = null,
        OverallStatusAggregator? aggregator = null,
        HardwareStatusManager? hardwareStatusManager = null)
    {
        // Load settings from TOML
        _config =
            config
            ?? ConfigurationLoader.Load();

        DevicePath = "SimulatorCashChanger";
        _hardwareStatusManager =
            hardwareStatusManager 
            ?? ServiceLocator.HardwareStatusManager
            ?? new HardwareStatusManager();

        _logger =
            LogProvider
            .CreateLogger<SimulatorCashChanger>();
        _logger
            .ZLogInformation(
                $"SimulatorCashChanger initialized.");

        _inventory =
            inventory 
            ?? ServiceLocator.Inventory 
            ?? new Inventory();
        
        if (inventory == null && ServiceLocator.Inventory == null)
        {
            var state =
                ConfigurationLoader
                .LoadInventoryState();
            if (state.Counts.Count > 0)
            {
                _inventory.LoadFromDictionary(state.Counts);
            }
            else
            {
                // Inventory から全ての通貨の金種をロード
                foreach (var currencyEntry in _config.Inventory)
                {
                    var currencyCode = currencyEntry.Key;
                    foreach (var item in currencyEntry.Value.Denominations)
                    {
                        if (DenominationKey.TryParse(item.Key, currencyCode, out var key) && key != null)
                        {
                            _inventory.SetCount(key, item.Value.InitialCount);
                        }
                    }
                }
            }
        }

        _history =
            history
            ?? ServiceLocator.History
            ?? new TransactionHistory();
        _manager =
            manager
            ?? ServiceLocator.Manager
            ?? new CashChangerManager(_inventory, _history);
        _depositController =
            depositController
            ?? new DepositController(_inventory, _config.Simulation, _hardwareStatusManager);
        _dispenseController =
            dispenseController
            ?? new DispenseController(_manager, _config.Simulation, _hardwareStatusManager);

        // Status monitors / Aggregator
        var monitors =
            _inventory
            .AllCounts
            .Select(kv =>
                (kv.Key,
                 _config.GetDenominationSetting(kv.Key)))
            .Select(x =>
                new CashStatusMonitor(
                    _inventory,
                    x.Key,
                    x.Item2.NearEmpty,
                    x.Item2.NearFull,
                    x.Item2.Full))
            .ToList();
        _statusAggregator =
            aggregator
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
                    _lastCashChangerStatus = newDeviceStatus;
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
                    _lastFullStatus = newFullStatus;
                    NotifyEvent(new StatusUpdateEventArgs((int)newFullStatus));
                }
            }),
            _hardwareStatusManager.IsJammed.Subscribe(jammed =>
            {
                if (jammed)
                {
                    _lastCashChangerStatus = CashChangerStatus.OK; // Property based status
                    NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.VendorJam));
                }
                else
                {
                    _lastCashChangerStatus = CashChangerStatus.OK;
                    NotifyEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.VendorOk));
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
                    NotifyEvent(new DataEventArgs(0));
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
        ThrowIfBusy();
        _depositController.BeginDeposit();
    }

    /// <summary>現金投入処理を終了します。</summary>
    public override void EndDeposit(CashDepositAction action) => _depositController.EndDeposit(action);

    /// <summary>投入された現金の計数を確定します。</summary>
    public override void FixDeposit() 
    {
        _depositController.FixDeposit();

        if (DataEventEnabled && CapDepositDataEvent)
        {
            QueueEvent(new DataEventArgs(0));
        }
    }

    /// <summary>現金投入処理を一時停止または再開します。</summary>
    public override void PauseDeposit(CashDepositPause control) => _depositController.PauseDeposit(control);

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

    /// <summary>指定された金額の釣銭を払い出します。</summary>
    public override void DispenseChange(int amount)
    {
        if (amount <= 0)
        {
            throw new PosControlException("Amount must be positive", ErrorCode.Illegal);
        }

        ThrowIfDepositInProgress();
        ThrowIfBusy();

        var factor = GetCurrencyFactor();
        var decimalAmount = amount / factor;

        async void OnComplete(ErrorCode code, int codeEx)
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
                _asyncProcessing = false; // State must be updated before firing the event
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
        ThrowIfDepositInProgress();
        ThrowIfBusy();

        var dict = new Dictionary<DenominationKey, int>();
        foreach (var cc in cashCounts)
        {
            var cashType = (cc.Type == CashCountType.Bill) 
                ? CashType.Bill 
                : CashType.Coin;

            var factor = GetCurrencyFactor(_activeCurrencyCode);
            var val = cc.NominalValue / factor;
            var key = new DenominationKey(val, cashType, _activeCurrencyCode);
            dict[key] = cc.Count;
        }

        async void OnComplete(ErrorCode code, int codeEx)
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

    // ========== ReadCashCounts ==========
    
    /// <summary>現在の現金在庫数を読み取ります。</summary>
    public override CashCounts ReadCashCounts() 
    {
        ThrowIfBusy();
        var sorted = _inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == _activeCurrencyCode)
            .OrderBy(kv => kv.Key.Type) // Coin(0) before Bill(1)
            .ThenBy(kv => kv.Key.Value);

        var list = sorted.Select(kv => new CashCount(
            (kv.Key.Type == CashType.Bill) ? CashCountType.Bill : CashCountType.Coin,
            GetNominalValue(kv.Key),
            kv.Value)).ToList();

        return new CashCounts([.. list], false);
    }

    /// <summary>ベンダー固有のコマンドをデバイスに送信します。</summary>
    public override DirectIOData DirectIO(int command, int data, object obj) => new(data, obj);

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
    public override bool CapRepayDeposit => false;

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
