using CashChangerSimulator.Device.PosForDotNet;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CashChangerSimulator.Tests.Device;

/// <summary>POS for .NET レイヤーのテストセットアップを共通化するための基底クラス。</summary>
public abstract class UposTestBase : IDisposable
{
    private bool _disposed;

    protected UposTestBase()
    {
        Changer = new InternalSimulatorCashChanger();
    }

    /// <summary>テスト対象のキャッシュチェンジャーインスタンス。</summary>
    protected InternalSimulatorCashChanger Changer { get; }

    /// <summary>全ての金種(JPY/USD)に対して一定数のキャッシュを補充します。</summary>
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
                try
                {
                    Changer.Dispose();
                }
                catch (Exception ex)
                {
                    // POS for .NET SDK 内部(StopListeningForGlobalEvents等)のクラッシュを無視し、
                    // テストの完走を優先させる。
                    System.Diagnostics.Debug.WriteLine($"[UposTestBase] Global SDK Dispose Error: {ex.Message}");
                }
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
