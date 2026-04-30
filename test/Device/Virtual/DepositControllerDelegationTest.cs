using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using Moq;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>DepositController の委譲動作を検証するテストクラス。</summary>
public class DepositControllerDelegationTest : DeviceTestBase
{
    private readonly Mock<CashChangerManager> managerMock;
    private readonly DepositController controller;

    public DepositControllerDelegationTest()
    {
        managerMock = new Mock<CashChangerManager>(Inventory, new TransactionHistory(), ConfigurationProvider);
        controller = new DepositController(managerMock.Object, Inventory, StatusManager, ConfigurationProvider, LoggerFactory, TimeProvider);
        StatusManager.Input.IsConnected.Value = true;
    }

    /// <summary>EndDeposit(NoChange) を呼び出した際、CashChangerManager の Deposit メソッドへ正しく委譲されることを検証します。</summary>
    [Fact]
    public void EndDepositNoChangeShouldDelegateToCashChangerManagerDeposit()
    {
        // Arrange
        controller.BeginDeposit();

        // Simulate adding cash
        var key = new DenominationKey(1000m, CurrencyCashType.Bill, "JPY");
        controller.RequiredAmount = 2000m;
        controller.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, 2 } });
        controller.FixDeposit();

        // Act
        controller.EndDeposit(DepositAction.NoChange);

        // Assert
        managerMock.Verify(
            m => m.Deposit(It.Is<IReadOnlyDictionary<DenominationKey, int>>(
            dict => dict.ContainsKey(key) && dict[key] == 2)), Times.Once);
    }
}
