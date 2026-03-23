using CashChangerSimulator.Core.Transactions;

namespace CashChangerSimulator.Core.Services;

/// <summary>
/// 取引履歴を特定の形式にエクスポートするためのサービスインターフェース。
/// </summary>
public interface IHistoryExportService
{
    /// <summary>
    /// 取引履歴を文字列形式にエクスポートします。
    /// </summary>
    /// <param name="entries">エクスポート対象の取引履歴エントリ。</param>
    /// <returns>エクスポートされた文字列データ。</returns>
    string Export(IEnumerable<TransactionEntry> entries);
}
