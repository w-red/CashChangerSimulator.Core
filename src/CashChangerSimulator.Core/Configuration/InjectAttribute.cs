namespace MicroResolver;

/// <summary>
/// MicroResolver が DI 用のコンストラクタを特定するための属性。
/// MicroResolver 本体への依存を避けるため、Core プロジェクト内で定義。
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class InjectAttribute : Attribute
{
}
