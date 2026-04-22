using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device.PosForDotNet.Services;

/// <summary>シミュレータの設定と言語・通貨状態を管理するクラス。</summary>
public sealed class UposConfigurationManager : IUposConfigurationManager, IDisposable
{
    private readonly ConfigurationProvider configProvider;
    private readonly Inventory inventory;
    private readonly IDeviceStateProvider stateProvider;
    private readonly ILogger<UposConfigurationManager> logger = LogProvider.CreateLogger<UposConfigurationManager>();
    private string activeCurrencyCode = "JPY";
    private string[] cachedCurrencyCodeList = [];
    private readonly CompositeDisposable disposables = [];

    /// <summary><see cref="UposConfigurationManager"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="configProvider">構成プロバイダー。</param>
    /// <param name="inventory">現金在庫。</param>
    /// <param name="stateProvider">デバイス状態プロバイダー。</param>
    public UposConfigurationManager(
        ConfigurationProvider configProvider,
        Inventory inventory,
        IDeviceStateProvider stateProvider)
    {
        this.configProvider = configProvider;
        this.inventory = inventory;
        this.stateProvider = stateProvider;

        UpdateCachedCurrencyList();
        this.configProvider.Reloaded.Subscribe(_ => OnConfigurationReloaded()).AddTo(disposables);
    }

    /// <summary>現在の通貨コードを取得または設定します。</summary>
    public string CurrencyCode
    {
        get => activeCurrencyCode;
        set
        {
            if (!CurrencyCodeList.Contains(value))
            {
                throw new PosControlException($"Unsupported currency: {value}", ErrorCode.Illegal);
            }

            activeCurrencyCode = value;
        }
    }

    /// <summary>サポートされている通貨コードのリストを取得します。</summary>
    public string[] CurrencyCodeList => cachedCurrencyCodeList;

    /// <summary>入金可能な通貨コードのリストを取得します。</summary>
    public string[] DepositCodeList => cachedCurrencyCodeList;

    private void UpdateCachedCurrencyList()
    {
        cachedCurrencyCodeList = configProvider.Config.Inventory.Keys.ToArray();
    }

    /// <summary>現在の通貨に対応する現金単位を取得します。</summary>
    public CashUnits CurrencyCashList => UposCurrencyHelper.BuildCashUnits(inventory, activeCurrencyCode);

    /// <summary>入金に対応する現金単位を取得します。</summary>
    public CashUnits DepositCashList => CurrencyCashList;

    /// <summary>構成マネージャーを初期化します。</summary>
    public void Initialize()
    {
        UpdateCachedCurrencyList();
        activeCurrencyCode = CurrencyCodeList.FirstOrDefault() ?? "JPY";
    }

    private bool disposed;

    /// <summary>構成情報を再読み込みします。</summary>
    public void Reload()
    {
        if (disposed)
        {
            return;
        }

        configProvider.Reload();
        logger.ZLogInformation($"Configuration reloaded in UposConfigurationManager.");
        UpdateSimulatorState();
    }

    private void OnConfigurationReloaded()
    {
        if (disposed)
        {
            return;
        }

        logger.ZLogInformation($"Configuration reloaded in UposConfigurationManager.");
        UpdateSimulatorState();
    }

    private void UpdateSimulatorState()
    {
        if (disposed)
        {
            return;
        }

        UpdateCachedCurrencyList();

        // Re-detect active currency if current one is gone
        if (!CurrencyCodeList.Contains(activeCurrencyCode))
        {
            activeCurrencyCode = CurrencyCodeList.FirstOrDefault() ?? "JPY";
        }

        // Clear inventory to avoid cross-currency pollution
        // ONLY if device is open, to avoid interference with startup sequence
        if (stateProvider.State != PosSharp.Abstractions.ControlState.Closed)
        {
            inventory.Clear();
        }

        logger.ZLogInformation($"Simulator state updated. Active Currency: {activeCurrencyCode}");
    }

    /// <summary>リソースを解放します。</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
