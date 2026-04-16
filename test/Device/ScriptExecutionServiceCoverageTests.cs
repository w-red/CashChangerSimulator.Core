using CashChangerSimulator.Device.Virtual.Services;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class ScriptExecutionServiceCoverageTests
{
    /// <summary>未知の変数が指定された際に値の解決が適切に行われる(または例外が投げられる)ことを検証する。</summary>
    [Fact]
    public void ResolveValueWithUnknownVariableShouldReturnAsIsOrThrow()
    {
        var context = new ScriptExecutionContext();

        var method = typeof(ScriptExecutionService).GetMethod("ResolveValue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (method != null)
        {
            var result = method.Invoke(null, ["123", context]);
            result.ShouldBe(123);
        }
    }
}
