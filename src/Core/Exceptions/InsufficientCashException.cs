namespace CashChangerSimulator.Core.Exceptions;

/// <summary>在庫不足などにより、要求された金額を放出できない場合に投げられる例外。</summary>
/// <param name="message">例外メッセージ。</param>
public class InsufficientCashException(string message) : Exception(message)
{
}
