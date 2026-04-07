namespace CashChangerSimulator.Core.Services;

/// <summary>ユーザーに通知や警告を表示するためのサービスインターフェース。</summary>
public interface INotifyService
{
    /// <summary>警告メッセージを表示します。</summary>
    /// <param name="message">表示するメッセージ。</param>
    /// <param name="title">ダイアログのタイトル。</param>
    void ShowWarning(string message, string title = "Warning");

    /// <summary>エラーメッセージを表示します。</summary>
    /// <param name="message">表示するメッセージ。</param>
    /// <param name="title">ダイアログのタイトル。</param>
    void ShowError(string message, string title = "Error");

    /// <summary>情報メッセージを表示します。</summary>
    /// <param name="message">表示するメッセージ。</param>
    /// <param name="title">ダイアログのタイトル。</param>
    void ShowInfo(string message, string title = "Info");
}
