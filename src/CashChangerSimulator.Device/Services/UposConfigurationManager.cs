using System;
using System.Linq;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using CashChangerSimulator.Core;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device.Services;

/// <summary>シミュレータの設定と言語・通貨状態を管理するクラス。</summary>
public class UposConfigurationManager : IUposConfigurationManager, IDisposable
{
    private readonly ConfigurationProvider _configProvider;
    private readonly Inventory _inventory;
    private readonly IDeviceStateProvider _stateProvider;
    private readonly ILogger<UposConfigurationManager> _logger = LogProvider.CreateLogger<UposConfigurationManager>();
    private string _activeCurrencyCode = "JPY";
    private readonly IDisposable _subscription;

    public UposConfigurationManager(
        ConfigurationProvider configProvider,
        Inventory inventory,
        IDeviceStateProvider stateProvider)
    {
        _configProvider = configProvider;
        _inventory = inventory;
        _stateProvider = stateProvider;

        _subscription = _configProvider.Reloaded.Subscribe(_ => OnConfigurationReloaded());
    }

    public string CurrencyCode
    {
        get => _activeCurrencyCode;
        set
        {
            if (!CurrencyCodeList.Contains(value))
            {
                throw new PosControlException($"Unsupported currency: {value}", ErrorCode.Illegal);
            }
            _activeCurrencyCode = value;
        }
    }

    public string[] CurrencyCodeList => _configProvider.Config.Inventory.Keys.ToArray();
    public string[] DepositCodeList => CurrencyCodeList;

    public CashUnits CurrencyCashList => UposCurrencyHelper.BuildCashUnits(_inventory, _activeCurrencyCode);

    public CashUnits DepositCashList => CurrencyCashList;

    public void Initialize()
    {
        _activeCurrencyCode = CurrencyCodeList.FirstOrDefault() ?? "JPY";
    }

    public void Reload()
    {
        _configProvider.Reload();
        _logger.ZLogInformation($"Configuration reloaded in UposConfigurationManager.");
        UpdateSimulatorState();
    }

    private void OnConfigurationReloaded()
    {
        _logger.ZLogInformation($"Configuration reloaded in UposConfigurationManager.");
        UpdateSimulatorState();
    }

    private void UpdateSimulatorState()
    {
        // Re-detect active currency if current one is gone
        if (!CurrencyCodeList.Contains(_activeCurrencyCode))
        {
            _activeCurrencyCode = CurrencyCodeList.FirstOrDefault() ?? "JPY";
        }
        
        // Clear inventory to avoid cross-currency pollution
        // ONLY if device is open, to avoid interference with startup sequence
        if (_stateProvider.State != ControlState.Closed)
        {
            _inventory.Clear();
        }
        
        _logger.ZLogInformation($"Simulator state updated. Active Currency: {_activeCurrencyCode}");
    }

    public void Dispose()
    {
        _subscription.Dispose();
        GC.SuppressFinalize(this);
    }
}
