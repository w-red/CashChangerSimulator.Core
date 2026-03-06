using System;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>
/// UPOS 操作をカプセル化するコマンドインターフェース。
/// </summary>
public interface IUposCommand
{
    /// <summary>コマンドを実行します。</summary>
    void Execute();

    /// <summary>コマンドの実行可能性を検証します。</summary>
    /// <param name="mediator">検証に使用するメディエーター。</param>
    /// <param name="skipStateVerification">状態検証をスキップするかどうか。</param>
    void Verify(IUposMediator mediator, bool skipStateVerification);
}
