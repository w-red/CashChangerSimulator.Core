namespace CashChangerSimulator.Core.Services;

/// <summary>ユーザーに通知や警告を表示するためのサービスインターフェース。</summary>
public interface INotifyService
{
    /// <summary>
    /// 警告メッセージを表示します。
    /// </summary>
    /// <param name="message">表示するメッセージ。</param>
    /// <param name="title">ダイアログのタイトル。</param>
    void ShowWarning(string message, string title);
}
