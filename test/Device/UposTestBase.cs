using CashChangerSimulator.Device.PosForDotNet;

namespace CashChangerSimulator.Tests.Device;

/// <summary>
/// POS for .NET レイヤー (UPOS アダプター) のテストセットアップを共通化するための基底クラス。
/// </summary>
public abstract class UposTestBase : IDisposable
{
    private bool _disposed;

    protected UposTestBase()
    {
        Changer = new InternalSimulatorCashChanger();
    }

    /// <summary>テスト対象のキャッシュチェンジャーインスタンス。</summary>
    protected InternalSimulatorCashChanger Changer { get; }

    /// <summary>
    /// 全ての金種（JPY/USD）に対して一定数（デフォルト 10枚）のキャッシュを補充します。
    /// </summary>
    protected void SeedInitialCash(int count = 10)
    {
        foreach (var ccy in new[] { "JPY", "USD" })
        {
            foreach (int val in new[] { 10000, 5000, 2000, 1000 })
            {
                Changer.Inventory.SetCount(new CashChangerSimulator.Core.Models.DenominationKey(val, CashChangerSimulator.Core.Models.CurrencyCashType.Bill, ccy), count);
            }

            foreach (int val in new[] { 500, 100, 50, 10, 5, 1 })
            {
                Changer.Inventory.SetCount(new CashChangerSimulator.Core.Models.DenominationKey(val, CashChangerSimulator.Core.Models.CurrencyCashType.Coin, ccy), count);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Changer.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
