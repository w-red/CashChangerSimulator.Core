using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Core.Services;

/// <summary>
/// 釣銭機デバイスの基本操作を抽象化するインターフェース。
/// UPOS (Japanese Cash Changer) の論理操作をモデル化します。
/// </summary>
public interface ICashChangerDevice : IDisposable
{
    // 論理状態管理
    bool IsConnected { get; }
    bool Claimed { get; }
    bool DeviceEnabled { get; }

    // 基本制御
    void Open();
    void Close();
    void Claim(int timeout);
    void Release();
    void Enable();
    void Disable();

    // 入出金操作
    void Deposit(IReadOnlyDictionary<DenominationKey, int> counts);
    void Dispense(decimal amount, string? currencyCode = null);

    // 在庫管理
    IReadOnlyDictionary<DenominationKey, int> GetInventory();
}
