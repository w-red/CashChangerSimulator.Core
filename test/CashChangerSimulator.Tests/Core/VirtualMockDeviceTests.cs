using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Core;

/// <summary>
/// VirtualMockDevice の機能検証テスト。
/// </summary>
public class VirtualMockDeviceTests
{
    private readonly VirtualMockDevice _device1;
    private readonly VirtualMockDevice _device2;
    private readonly Mock<ILogger<VirtualMockDevice>> _loggerMock;
    private readonly HardwareStatusManager _statusManager;

    public VirtualMockDeviceTests()
    {
        var inventory = new Inventory();
        var history = new TransactionHistory();
        _statusManager = new HardwareStatusManager();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        _loggerMock = new Mock<ILogger<VirtualMockDevice>>();

        _device1 = new VirtualMockDevice(manager, inventory, _statusManager, _loggerMock.Object);
        _device2 = new VirtualMockDevice(manager, inventory, _statusManager, _loggerMock.Object);
    }

    /// <summary>
    /// 他のプロセス（インスタンス）がデバイスを占有している場合、Claim が失敗することを検証します。
    /// </summary>
    [Fact]
    public void ConcurrentClaim_ShouldThrowException()
    {
        // Arrange
        _device1.Open();
        _device2.Open();

        // Act & Assert
        _device1.Claim(100);

        // 別スレッドで _device2.Claim を実行し、例外を確認する
        var task = Task.Run(() => _device2.Claim(100));
        
        // Assert: 別スレッドからの Claim は失敗するはず
        var ex = Should.Throw<Exception>(async () => await task);
        ex.Message.ShouldContain("claimed", Case.Insensitive);
    }

    /// <summary>
    /// Release 後は他のインスタンスが Claim 可能になることを検証します。
    /// </summary>
    [Fact]
    public void ClaimAfterRelease_ShouldSucceed()
    {
        // Arrange
        _device1.Open();
        _device2.Open();

        // Act
        _device1.Claim(100);
        _device1.Release();

        // Assert
        _device2.Claim(100);
        _device2.Claimed.ShouldBeTrue();
    }
}
