using CashChangerSimulator.UI.Wpf.Services;

namespace CashChangerSimulator.Tests.UI;

/// <summary>
/// A dispatcher service for testing that executes actions immediately.
/// </summary>
public class ImmediateDispatcherService : IDispatcherService
{
    public void SafeInvoke(Action action)
    {
        action();
    }

    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public object? GetActiveWindow()
    {
        return null;
    }
}
