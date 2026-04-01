namespace CashChangerSimulator.Core;

/// <summary>シミュレータのサービス解決を抽象化するインターフェース。</summary>
public interface ISimulatorServiceProvider
{
    /// <summary>指定した型のサービスインスタンスを解決する。</summary>
    T Resolve<T>() where T : class;
}
