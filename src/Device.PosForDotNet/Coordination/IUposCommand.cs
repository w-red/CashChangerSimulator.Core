namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>UPOS 操作をカプセル化するコマンドインターフェース。</summary>
public interface IUposCommand
{
    /// <summary>コマンドを実行します。</summary>
    void Execute();

    /// <summary>コマンドを非同期で実行します。</summary>
    Task ExecuteAsync();

    /// <summary>コマンドの実行可能性を検証します。</summary>
    /// <param name="mediator">検証に使用するメディエーター。</param>
    void Verify(IUposMediator mediator);
}
