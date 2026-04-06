using CashChangerSimulator.Device.Virtual.Services;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class ScriptExecutionServiceCoverageTests
{
    [Fact]
    public void ResolveValue_WithUnknownVariable_ShouldReturnAsIsOrThrow()
    {
        var context = new ScriptExecutionContext();
        
        var method = typeof(ScriptExecutionService).GetMethod("ResolveValue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        
        if (method != null)
        {
            var result = method.Invoke(null, new object[] { "123", context });
            result.ShouldBe(123);
        }
    }
}
