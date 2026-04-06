namespace CashChangerSimulator.Core;

/// <summary>シミュレータのサービス解決を抽象化するインターフェース。.</summary>
public interface ISimulatorServiceProvider
{
    /// <summary>指定した型のサービスインスタンスを解決する。.</summary>
    /// <typeparam name="T">解決するサービスの型。.</typeparam>
    /// <returns>解決されたサービスの実体。.</returns>
    T Resolve<T>()
        where T : class;
}
