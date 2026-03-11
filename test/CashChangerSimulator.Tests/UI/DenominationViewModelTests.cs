using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.UI;

public class DenominationViewModelTests
{
    [Fact]
    public void BreakdownProperties_ShouldUpdate_WhenInventoryChanges()
    {
        // Arrange
        var inv = new Inventory();
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var configProvider = new ConfigurationProvider();
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitor = new CashStatusMonitor(inv, key, 5, 100, 200);
        var depositController = new DepositController(inv);
        
        var facadeMock = new Mock<IDeviceFacade>();
        facadeMock.Setup(f => f.Inventory).Returns(inv);
        facadeMock.Setup(f => f.Deposit).Returns(depositController);
        
        var vm = new DenominationViewModel(facadeMock.Object, key, metadataProvider, monitor, configProvider);

        // Act & Assert: Recyclable (Normal)
        inv.Add(key, 5);
        vm.Count.Value.ShouldBe(5);
        vm.RecyclableCount.Value.ShouldBe(5);

        // Act & Assert: Collection (Overflow)
        inv.AddCollection(key, 3);
        vm.Count.Value.ShouldBe(8);
        vm.CollectionCount.Value.ShouldBe(3);

        // Act & Assert: Reject
        inv.AddReject(key, 2);
        vm.Count.Value.ShouldBe(10);
        vm.RejectCount.Value.ShouldBe(2);
    }
}
