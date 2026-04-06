namespace CashChangerSimulator.Core.Exceptions;

/// <summary>在庫不足などにより、要求された金額を放出できない場合に投げられる例外。</summary>
public class InsufficientCashException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="InsufficientCashException"/> class.空のインスタンスを初期化する。</summary>
    public InsufficientCashException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InsufficientCashException"/> class.メッセージを指定して初期化する。</summary>
    /// <param name="message">例外メッセージ。</param>
    public InsufficientCashException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InsufficientCashException"/> class.メッセージと内部例外を指定して初期化する。</summary>
    /// <param name="message">例外メッセージ。</param>
    /// <param name="innerException">内部例外。</param>
    public InsufficientCashException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
