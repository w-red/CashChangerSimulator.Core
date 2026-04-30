using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CashChangerSimulator.Tests.Fixtures;

public class ControllerTestBuilder
{
    private readonly CashChangerFixture _fixture;
    private Mock<IDeviceSimulator>? _simulatorMock;

    public ControllerTestBuilder(CashChangerFixture fixture)
    {
        _fixture = fixture;
    }

    public ControllerTestBuilder WithConnected(bool connected = true)
    {
        _fixture.StatusManager.Input.IsConnected.Value = connected;
        return this;
    }

    public ControllerTestBuilder WithInitialCash(DenominationKey key, int count)
    {
        _fixture.Inventory.SetCount(key, count);
        return this;
    }

    public ControllerTestBuilder WithSimulator(Mock<IDeviceSimulator> simulatorMock)
    {
        _simulatorMock = simulatorMock;
        return this;
    }

    public DepositController BuildDepositController()
    {
        return new DepositController(
            _fixture.Manager,
            _fixture.Inventory,
            _fixture.StatusManager,
            _fixture.ConfigurationProvider,
            _fixture.LoggerFactory ?? NullLoggerFactory.Instance,
            _fixture.TimeProvider);
    }

    public DispenseController BuildDispenseController()
    {
        var simulator = _simulatorMock?.Object ?? new Mock<IDeviceSimulator>().Object;
        return new DispenseController(
            _fixture.Manager,
            _fixture.Inventory,
            _fixture.ConfigurationProvider,
            _fixture.LoggerFactory ?? NullLoggerFactory.Instance,
            _fixture.StatusManager,
            simulator);
    }
}
