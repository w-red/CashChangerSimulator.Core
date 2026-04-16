using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CashChangerSimulator.Tests.Fixtures;

/// <summary>
/// コントローラーの生成を Fluent API で支援するビルダー。
/// </summary>
public class ControllerTestBuilder
{
    private readonly CashChangerFixture _fixture;
    private Mock<IDeviceSimulator>? _simulatorMock;

    public ControllerTestBuilder(CashChangerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>デフォルトの接続状態で開始します。</summary>
    public ControllerTestBuilder WithConnected(bool connected = true)
    {
        _fixture.StatusManager.Input.IsConnected.Value = connected;
        return this;
    }

    /// <summary>初期残高を設定します。</summary>
    public ControllerTestBuilder WithInitialCash(DenominationKey key, int count)
    {
        _fixture.Inventory.SetCount(key, count);
        return this;
    }

    /// <summary>シミュレータのモックを設定します。</summary>
    public ControllerTestBuilder WithSimulator(Mock<IDeviceSimulator> simulatorMock)
    {
        _simulatorMock = simulatorMock;
        return this;
    }

    /// <summary>DepositController をビルドします。</summary>
    public DepositController BuildDepositController()
    {
        return new DepositController(
            _fixture.Inventory,
            _fixture.StatusManager,
            _fixture.Manager,
            _fixture.ConfigurationProvider,
            _fixture.TimeProvider);
    }

    /// <summary>DispenseController をビルドします。</summary>
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
