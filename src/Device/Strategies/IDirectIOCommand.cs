using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>DirectIO コマンドの各戦略を表すインターフェース。</summary>
public interface IDirectIOCommand
{
    /// <summary>この戦略が対応するコマンドコード。</summary>
    int CommandCode { get; }

    /// <summary>コマンドを実行します。</summary>
    /// <param name="data">コマンドデータ。</param>
    /// <param name="obj">コマンドオブジェクト。</param>
    /// <param name="device">実行対象のデバイス（必要に応じて状態変更に使用）。</param>
    /// <returns>実行結果データ。</returns>
    DirectIOData Execute(int data, object obj, SimulatorCashChanger device);
}
