using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>VirtualMockDevice の機能検証テスト（オープン、クローズ、占有、有効化等）。.</summary>
public class VirtualMockDeviceTests
{
    private readonly VirtualMockDevice device1;
    private readonly VirtualMockDevice device2;
    private readonly Mock<ILogger<VirtualMockDevice>> loggerMock;
    private readonly HardwareStatusManager statusManager;

    public VirtualMockDeviceTests()
    {
        var inventory = new Inventory();
        var history = new TransactionHistory();
        statusManager = new HardwareStatusManager();
        var manager = new CashChangerManager(inventory, history, (object?)null, null);
        loggerMock = new Mock<ILogger<VirtualMockDevice>>();

        device1 = new VirtualMockDevice(manager, inventory, statusManager, loggerMock.Object);
        device2 = new VirtualMockDevice(manager, inventory, statusManager, loggerMock.Object);
    }

    /// <summary>他のプロセス（インスタンス）がデバイスを占有している場合、Claim が失敗することを検証します。.</summary>
    [Fact]
    public void ConcurrentClaimShouldThrowException()
    {
        // Arrange
        device1.Open();
        device2.Open();

        // Act & Assert
        device1.Claim(100);

        // 別スレッドで _device2.Claim を実行し、例外を確認する
        var task = Task.Run(() => device2.Claim(100), TestContext.Current.CancellationToken);

        // Assert: 別スレッドからの Claim は失敗するはず
        var ex = Should.Throw<Exception>(async () => await task.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false));
        ex.Message.ShouldContain("claimed", Case.Insensitive);
    }

    /// <summary>Release 後は他のインスタンスが Claim 可能になることを検証します。.</summary>
    [Fact]
    public void ClaimAfterReleaseShouldSucceed()
    {
        // Arrange
        device1.Open();
        device2.Open();

        // Act
        device1.Claim(100);
        device1.Release();

        // Assert
        device2.Claim(100);
        device2.Claimed.ShouldBeTrue();
    }

    /// <summary>Open メソッドにより接続状態（IsConnected）が有効になることを検証します。.</summary>
    [Fact]
    public void OpenShouldSetConnected()
    {
        device1.Open();
        device1.IsConnected.ShouldBeTrue();
    }

    /// <summary>Close メソッドにより、切断状態および非有効化状態に遷移することを検証します。.</summary>
    [Fact]
    public void CloseShouldSetDisconnectedAndDisabled()
    {
        device1.Open();
        device1.Claim(100);
        device1.Enable();

        device1.Close();

        device1.IsConnected.ShouldBeFalse();
        device1.DeviceEnabled.ShouldBeFalse();
        device1.Claimed.ShouldBeFalse();
    }

    /// <summary>占有（Claim）されている状態で Enable が成功することを検証します。.</summary>
    [Fact]
    public void EnableShouldSucceedWhenClaimed()
    {
        device1.Open();
        device1.Claim(100);
        device1.Enable();
        device1.DeviceEnabled.ShouldBeTrue();
    }

    /// <summary>占有（Claim）されていない状態で Enable を呼び出すと例外が発生することを検証します。.</summary>
    [Fact]
    public void EnableShouldThrowWhenNotClaimed()
    {
        device1.Open();
        Should.Throw<InvalidOperationException>(() => device1.Enable());
    }

    /// <summary>デバイスが有効化（Enable）されていない状態で入金操作を行うと例外が発生することを検証します。.</summary>
    [Fact]
    public void DepositShouldThrowWhenNotEnabled()
    {
        device1.Open();
        device1.Claim(100);

        // Not enabled
        Should.Throw<InvalidOperationException>(() => device1.Deposit(new Dictionary<DenominationKey, int>()));
    }

    /// <summary>デバイスが有効化（Enable）されていない状態で出金操作を行うと例外が発生することを検証します。.</summary>
    [Fact]
    public void DispenseShouldThrowWhenNotEnabled()
    {
        device1.Open();
        device1.Claim(100);

        // Not enabled
        Should.Throw<InvalidOperationException>(() => device1.Dispense(1000));
    }

    /// <summary>現在の在庫情報を正しく取得できることを検証します。.</summary>
    [Fact]
    public void GetInventoryShouldReturnCorrectData()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        device1.Open();
        device1.Claim(100);
        device1.Enable();
        device1.Deposit(new Dictionary<DenominationKey, int> { { key, 5 } });

        var inventory = device1.GetInventory();
        inventory[key].ShouldBe(5);
    }
}
