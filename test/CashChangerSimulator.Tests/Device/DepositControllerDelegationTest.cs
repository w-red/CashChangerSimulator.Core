using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Moq;

namespace CashChangerSimulator.Tests.Device;

/// <summary>
/// <see cref="DepositController"/> が直接在庫を更新せず、
/// <see cref="CashChangerManager"/> に入金履歴の記録と在庫の更新を委譲することを検証するテストクラス。
/// </summary>
public class DepositControllerDelegationTest
{
    /// <summary>EndDeposit(Store) を呼び出した際、CashChangerManager の Deposit メソッドへ正しく委譲されることを検証します。</summary>
    [Fact]
    public void EndDeposit_Store_ShouldDelegateToCashChangerManagerDeposit()
    {
        // Arrange
        var inventory = new Inventory();
        var hardwareManager = new HardwareStatusManager();
        hardwareManager.SetConnected(true);
        var managerMock =
            new Mock<CashChangerManager>(
                inventory,
                new TransactionHistory(),
                new ChangeCalculator());

        var controller = new DepositController(inventory, hardwareManager, managerMock.Object);

        controller.BeginDeposit();
        // Simulate adding cash
        var key = new DenominationKey(1000m, CurrencyCashType.Bill, "JPY");
        controller.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, 2 } });
        controller.FixDeposit();

        // Act
        controller.EndDeposit(CashDepositAction.NoChange);

        // Assert
        managerMock.Verify(m => m.Deposit(It.Is<IReadOnlyDictionary<DenominationKey, int>>(
            dict => dict.ContainsKey(key) && dict[key] == 2
        )), Times.Once);
    }
}
