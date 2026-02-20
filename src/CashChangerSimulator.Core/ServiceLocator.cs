using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Core;

/// <summary>プロジェクトを跨いでシングルトンインスタンスを共有するための簡易サービスロケーター。</summary>
public static class ServiceLocator
{
    public static Inventory? Inventory { get; set; }
    public static TransactionHistory? History { get; set; }
    public static CashChangerManager? Manager { get; set; }
    public static HardwareStatusManager? HardwareStatusManager { get; set; }
}
