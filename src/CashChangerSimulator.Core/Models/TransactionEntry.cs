using System;
using System.Collections.Generic;

namespace CashChangerSimulator.Core.Models;

/// <summary>
/// 1回の取引（履歴）を表すレコード。
/// </summary>
/// <param name="Timestamp">取引日時。</param>
/// <param name="Type">取引種別。</param>
/// <param name="Amount">合計金額の変動量。</param>
/// <param name="Counts">金種ごとの枚数変動（DenominationKey, 枚数）。</param>
public record TransactionEntry(
    DateTimeOffset Timestamp,
    TransactionType Type,
    decimal Amount,
    IReadOnlyDictionary<DenominationKey, int> Counts
);
