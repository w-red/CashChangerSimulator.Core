using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Services;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>SimulatorCashChanger の初期化に必要な依存関係をまとめる Parameter Object。</summary>
/// <remarks>
/// 各種コントローラー、マネージャー、およびサービスを一括して保持し、
/// シミュレータ本体への依存性注入を簡素化するために使用されます。
/// </remarks>
/// <param name="ConfigProvider">設定プロバイダー。</param>
/// <param name="Inventory">在庫管理モデル。</param>
/// <param name="History">取引履歴管理。</param>
/// <param name="Manager">デバイス操作マネージャー。</param>
/// <param name="DepositController">入金コントローラー。</param>
/// <param name="DispenseController">出金コントローラー。</param>
/// <param name="AggregatorProvider">ステータス集約プロバイダー。</param>
/// <param name="HardwareStatusManager">ハードウェア状態マネージャー。</param>
/// <param name="DiagnosticController">診断コントローラー。</param>
/// <param name="Mediator">UPOS メディエーター。</param>
/// <param name="ConfigurationManager">UPOS 設定マネージャー。</param>
/// <param name="EventNotifier">UPOS イベント通知サービス。</param>
public record SimulatorDependencies(
    ConfigurationProvider? ConfigProvider = null,
    Inventory? Inventory = null,
    TransactionHistory? History = null,
    CashChangerManager? Manager = null,
    DepositController? DepositController = null,
    DispenseController? DispenseController = null,
    OverallStatusAggregatorProvider? AggregatorProvider = null,
    HardwareStatusManager? HardwareStatusManager = null,
    DiagnosticController? DiagnosticController = null,
    IUposMediator? Mediator = null,
    IUposConfigurationManager? ConfigurationManager = null,
    IUposEventNotifier? EventNotifier = null,
    string? GlobalLockFilePath = null
);
