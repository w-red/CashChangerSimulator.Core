using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device.PosForDotNet.Services;

/// <summary>シミュレータの設定と言語・通貨状態を管理するクラス。</summary>
public class UposConfigurationManager : IUposConfigurationManager, IDisposable
{
    private readonly ConfigurationProvider configProvider;
    private readonly Inventory inventory;
    private readonly IDeviceStateProvider stateProvider;
    private readonly ILogger<UposConfigurationManager> logger = LogProvider.CreateLogger<UposConfigurationManager>();
    private string activeCurrencyCode = "JPY";
    private readonly IDisposable subscription;

    /// <inheritdoc/>
    public UposConfigurationManager(
        ConfigurationProvider configProvider,
        Inventory inventory,
        IDeviceStateProvider stateProvider)
    {
        this.configProvider = configProvider;
        this.inventory = inventory;
        this.stateProvider = stateProvider;

        subscription = this.configProvider.Reloaded.Subscribe(_ => OnConfigurationReloaded());
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public string[] CurrencyCodeList => configProvider.Config.Inventory.Keys.ToArray();
    /// <inheritdoc/>
    public string[] DepositCodeList => CurrencyCodeList;

    /// <inheritdoc/>
    public CashUnits CurrencyCashList => UposCurrencyHelper.BuildCashUnits(inventory, activeCurrencyCode);

    /// <inheritdoc/>
    public CashUnits DepositCashList => CurrencyCashList;

    /// <inheritdoc/>
    public void Initialize()
    {
        activeCurrencyCode = CurrencyCodeList.FirstOrDefault() ?? "JPY";
    }

    private bool disposed;

    /// <inheritdoc/>
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

        // Re-detect active currency if current one is gone
        if (!CurrencyCodeList.Contains(activeCurrencyCode))
        {
            activeCurrencyCode = CurrencyCodeList.FirstOrDefault() ?? "JPY";
        }

        // Clear inventory to avoid cross-currency pollution
        // ONLY if device is open, to avoid interference with startup sequence
        if (stateProvider.State != DeviceControlState.Closed)
        {
            inventory.Clear();
        }

        logger.ZLogInformation($"Simulator state updated. Active Currency: {activeCurrencyCode}");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        subscription.Dispose();
        GC.SuppressFinalize(this);
    }
}
