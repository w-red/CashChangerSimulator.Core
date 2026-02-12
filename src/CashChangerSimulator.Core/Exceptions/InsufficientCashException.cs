using System;

namespace CashChangerSimulator.Core.Exceptions;

/// <summary>
/// 在庫不足などにより、要求された金額を放出できない場合に投げられる例外。
/// </summary>
public class InsufficientCashException : Exception
{
    public InsufficientCashException(string message) : base(message) { }
}
