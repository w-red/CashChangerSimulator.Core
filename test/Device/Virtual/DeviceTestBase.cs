using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace CashChangerSimulator.Tests.Device.Virtual;

public abstract class DeviceTestBase : IDisposable
{
    private bool disposed;

    protected DeviceTestBase()
    {
        Fixture = new CashChangerFixture();
        
        Fixture.ConfigurationProvider.Config.Simulation.DispenseDelayMs = 1000;
        Fixture.ConfigurationProvider.Config.Simulation.DepositDelayMs = 1000;
        
        LoggerFactoryMock = new Mock<ILoggerFactory>();
        LoggerFactory = NullLoggerFactory.Instance;
        DeviceFactory = new VirtualCashChangerDeviceFactory(ConfigurationProvider, LoggerFactory, TimeProvider);
    }

    protected CashChangerFixture Fixture { get; }

    protected Inventory Inventory => Fixture.Inventory;
    protected TransactionHistory History => Fixture.History;
    protected CashChangerManager Manager => Fixture.Manager;
    protected ConfigurationProvider ConfigurationProvider => Fixture.ConfigurationProvider;
    
    // Fixed: explicit cast to FakeTimeProvider if stored as TimeProvider in Fixture
    // Or ensure Fixture stores it as FakeTimeProvider for tests.
    public FakeTimeProvider TimeProvider => (FakeTimeProvider)Fixture.TimeProvider;

    protected HardwareStatusManager StatusManager => Fixture.StatusManager;
    protected Mock<ILoggerFactory> LoggerFactoryMock { get; }
    protected ILoggerFactory LoggerFactory { get; }
    protected VirtualCashChangerDeviceFactory DeviceFactory { get; }

    protected string GenerateUniqueMutexName() => $"Global\\TestMutex_{Guid.NewGuid()}";

    protected ICashChangerDevice CreateDevice(string? mutexName = null)
    {
        return DeviceFactory.Create(Manager, Inventory, StatusManager, mutexName ?? GenerateUniqueMutexName());
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                Fixture.Dispose();
            }
            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected async Task WaitUntil(Func<bool> condition, int timeoutSeconds = 5)
    {
        var startTimestamp = TimeProvider.GetTimestamp();
        var timeoutTicks = (long)(TimeSpan.FromSeconds(timeoutSeconds).TotalSeconds * 10_000_000); // 1 tick = 100ns, 1s = 10M ticks

        while (!condition())
        {
            var elapsedTicks = TimeProvider.GetTimestamp() - startTimestamp;
            if (elapsedTicks > timeoutTicks)
            {
                if (condition()) return;
                throw new Xunit.Sdk.XunitException($"Condition was not met within {timeoutSeconds}s (virtual time)");
            }

            TimeProvider.Advance(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1).ConfigureAwait(false);
        }
    }
}
