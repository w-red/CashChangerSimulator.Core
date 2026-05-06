namespace CashChangerSimulator.Core.Services;

/// <summary>Null 許容型に対する関数型の操作を支援する拡張メソッドクラス。</summary>
public static class FunctionalExtensions
{
    /// <summary>オブジェクトが null でない場合に、指定されたアクションを実行します。</summary>
    /// <typeparam name="T">オブジェクトの型。</typeparam>
    /// <param name="obj">対象のオブジェクト。</param>
    /// <param name="action">実行するアクション。</param>
    public static void Apply<T>(this T? obj, Action<T> action) where T : class
    {
        ArgumentNullException.ThrowIfNull(action);

        if (obj is not null)
        {
            action(obj);
        }
    }
}
