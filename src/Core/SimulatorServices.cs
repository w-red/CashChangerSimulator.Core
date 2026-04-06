namespace CashChangerSimulator.Core;

/// <summary>シミュレータのサービスプロバイダーへの静的アクセスポイント。</summary>
public static class SimulatorServices
{
    /// <summary>Gets or sets サービスプロバイダー。アプリケーション起動時に設定される。</summary>
    public static ISimulatorServiceProvider? Provider { get; set; }

    /// <summary>サービスの解決を試みる。プロバイダーが未設定またはサービスが未登録の場合は null を返す。</summary>
    /// <typeparam name="T">解決するサービスの種類。</typeparam>
    /// <returns>解決されたサービス、または null。</returns>
    public static T? TryResolve<T>()
        where T : class
    {
        if (Provider == null)
        {
            return null;
        }

        try
        {
            return Provider.Resolve<T>();
        }
        catch (InvalidOperationException)
        {
            // 解決に失敗した場合は null を返す設計のため、例外を飲み込みます。
            return null;
        }
    }

    /// <summary>サービスを解決する。解決できない場合は例外をスローする。</summary>
    /// <typeparam name="T">解決するサービスの種類。</typeparam>
    /// <returns>解決されたサービス。</returns>
    /// <exception cref="InvalidOperationException">サービスが見つからない場合にスローされます。</exception>
    public static T Resolve<T>()
        where T : class
    {
        return TryResolve<T>() ?? throw new InvalidOperationException($"Service {typeof(T).Name} not found.");
    }
}
