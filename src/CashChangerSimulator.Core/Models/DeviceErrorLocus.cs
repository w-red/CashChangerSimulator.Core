namespace CashChangerSimulator.Core.Models;

/// <summary>デバイスエラーが発生した場所を指定します。</summary>
public enum DeviceErrorLocus
{
    /// <summary>なし、または不明な発生場所。</summary>
    None = 0,

    /// <summary>非同期出力操作中にエラーが発生しました。</summary>
    Output = 1,

    /// <summary>非同期入力操作中にエラーが発生しました。</summary>
    Input = 2,

    /// <summary>非同期入力操作中にエラーが発生し、1つ以上の入力メッセージが利用可能です。</summary>
    InputData = 3,
}
