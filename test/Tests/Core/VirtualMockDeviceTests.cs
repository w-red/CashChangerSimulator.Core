using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>VirtualMockDevice の機能検証テスト（オープン、クローズ、占有、有効化等）。</summary>
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

    /// <summary>他のプロセス（インスタンス）がデバイスを占有している場合、Claim が失敗することを検証します。</summary>
    [Fact]
    public void ConcurrentClaimShouldThrowException()
    {
        // Arrange
        _device1.Open();
        _device2.Open();

        // Act & Assert
        _device1.Claim(100);

        // 別スレッドで _device2.Claim を実行し、例外を確認する
        var task = Task.Run(() => _device2.Claim(100), TestContext.Current.CancellationToken);

        // Assert: 別スレッドからの Claim は失敗するはず
        var ex = Should.Throw<Exception>(async () => await task.WaitAsync(TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("claimed", Case.Insensitive);
    }

    /// <summary>Release 後は他のインスタンスが Claim 可能になることを検証します。</summary>
    [Fact]
    public void ClaimAfterReleaseShouldSucceed()
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

    /// <summary>Open メソッドにより接続状態（IsConnected）が有効になることを検証します。</summary>
    [Fact]
    public void OpenShouldSetConnected()
    {
        _device1.Open();
        _device1.IsConnected.ShouldBeTrue();
    }

    /// <summary>Close メソッドにより、切断状態および非有効化状態に遷移することを検証します。</summary>
    [Fact]
    public void CloseShouldSetDisconnectedAndDisabled()
    {
        _device1.Open();
        _device1.Claim(100);
        _device1.Enable();
        
        _device1.Close();
        
        _device1.IsConnected.ShouldBeFalse();
        _device1.DeviceEnabled.ShouldBeFalse();
        _device1.Claimed.ShouldBeFalse();
    }

    /// <summary>占有（Claim）されている状態で Enable が成功することを検証します。</summary>
    [Fact]
    public void EnableShouldSucceedWhenClaimed()
    {
        _device1.Open();
        _device1.Claim(100);
        _device1.Enable();
        _device1.DeviceEnabled.ShouldBeTrue();
    }

    /// <summary>占有（Claim）されていない状態で Enable を呼び出すと例外が発生することを検証します。</summary>
    [Fact]
    public void EnableShouldThrowWhenNotClaimed()
    {
        _device1.Open();
        Should.Throw<InvalidOperationException>(() => _device1.Enable());
    }

    /// <summary>デバイスが有効化（Enable）されていない状態で入金操作を行うと例外が発生することを検証します。</summary>
    [Fact]
    public void DepositShouldThrowWhenNotEnabled()
    {
        _device1.Open();
        _device1.Claim(100);
        // Not enabled
        Should.Throw<InvalidOperationException>(() => _device1.Deposit(new Dictionary<DenominationKey, int>()));
    }

    /// <summary>デバイスが有効化（Enable）されていない状態で出金操作を行うと例外が発生することを検証します。</summary>
    [Fact]
    public void DispenseShouldThrowWhenNotEnabled()
    {
        _device1.Open();
        _device1.Claim(100);
        // Not enabled
        Should.Throw<InvalidOperationException>(() => _device1.Dispense(1000));
    }

    /// <summary>現在の在庫情報を正しく取得できることを検証します。</summary>
    [Fact]
    public void GetInventoryShouldReturnCorrectData()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _device1.Open();
        _device1.Claim(100);
        _device1.Enable();
        _device1.Deposit(new Dictionary<DenominationKey, int> { { key, 5 } });

        var inventory = _device1.GetInventory();
        inventory[key].ShouldBe(5);
    }
}
