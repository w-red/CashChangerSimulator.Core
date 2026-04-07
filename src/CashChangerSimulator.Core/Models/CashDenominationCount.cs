namespace CashChangerSimulator.Core.Models;

/// <summary>特定の金種とその数量を保持します。</summary>
/// <param name="Denomination">金種。</param>
/// <param name="Count">枚数。</param>
public record CashDenominationCount(decimal Denomination, int Count);
