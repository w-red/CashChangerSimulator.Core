namespace CashChangerSimulator.Core;

/// <summary>シミュレータのサービスプロバイダーへの静的アクセスポイント。</summary>
public static class SimulatorServices
{
    /// <summary>サービスプロバイダー。アプリケーション起動時に設定される。</summary>
    public static ISimulatorServiceProvider? Provider { get; set; }

    /// <summary>サービスの解決を試みる。プロバイダーが未設定またはサービスが未登録の場合は null を返す。</summary>
    public static T? TryResolve<T>() where T : class
    {
        if (Provider == null) return null;
        try
        {
            return Provider.Resolve<T>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>サービスを解決する。解決できない場合は例外をスローする。</summary>
    public static T Resolve<T>() where T : class
    {
        return TryResolve<T>() ?? throw new InvalidOperationException($"Service {typeof(T).Name} not found.");
    }
}
