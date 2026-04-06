using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Core.Services;

/// <summary>
/// 釣銭機デバイスの基本操作を抽象化するインターフェース。
/// UPOS (Japanese Cash Changer) の論理操作をモデル化します。
/// </summary>
public interface ICashChangerDevice : IDisposable
{
    /// <summary>Gets a value indicating whether デバイスがオープンされているかどうかを取得します。</summary>
    bool IsConnected { get; }

    /// <summary>Gets a value indicating whether デバイスが排他権（Claim）を取得しているかどうかを取得します。</summary>
    bool Claimed { get; }

    /// <summary>Gets a value indicating whether デバイスが有効化（Enable）されているかどうかを取得します。</summary>
    bool DeviceEnabled { get; }

    /// <summary>デバイスをプログラム的にオープンします。</summary>
    void Open();

    /// <summary>デバイスをクローズし、リソースを解放します。</summary>
    void Close();

    /// <summary>デバイスの排他権を取得します。</summary>
    /// <param name="timeout">タイムアウト（ミリ秒）。</param>
    void Claim(int timeout);

    /// <summary>デバイスの排他権を解放します。</summary>
    void Release();

    /// <summary>デバイスを有効化し、入排金操作を可能にします。</summary>
    void Enable();

    /// <summary>デバイスを無効化します。</summary>
    void Disable();

    /// <summary>現金を投入します。</summary>
    /// <param name="counts">投入する金種と枚数。</param>
    void Deposit(IReadOnlyDictionary<DenominationKey, int> counts);

    /// <summary>現金を払い出します。</summary>
    /// <param name="amount">払い出す合計金額。</param>
    /// <param name="currencyCode">通貨コード（任意）。</param>
    void Dispense(decimal amount, string? currencyCode = null);

    /// <summary>現在の在庫情報を取得します。</summary>
    /// <returns>在庫情報のコピー。</returns>
    IReadOnlyDictionary<DenominationKey, int> GetInventory();
}
