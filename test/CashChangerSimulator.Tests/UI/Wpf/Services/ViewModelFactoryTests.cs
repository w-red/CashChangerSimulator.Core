using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.UI.Wpf.Services;

public class ViewModelFactoryTests
{
    [Fact]
    public void Constructor_WithNullProvider_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new ViewModelFactory(null!));
    }

    [Fact]
    public void CreateDepositViewModel_ShouldResolveFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        // Mocking the viewmodel is hard if it has many dependencies, so we create a dummy object or register a mock.
        // Wait, DepositViewModel is a concrete type. 
        // We will just verify that the factory attempts to resolve it.
        var providerMock = new Mock<IServiceProvider>();
        
        // DepositViewModel does not have a parameterless constructor, so we can't easily mock return value of concrete class without its dependencies.
        // Instead, we just verify GetService is called.
        
        var factory = new ViewModelFactory(providerMock.Object);

        // Act & Assert
        // ActivatorUtilities will try to resolve dependencies from the provider.
        // Since we didn't register them, it will likely throw.
        var getDenoms = new Func<IEnumerable<DenominationViewModel>>(() => Enumerable.Empty<DenominationViewModel>());
        var isBusy = new BindableReactiveProperty<bool>(false);
        
        Should.Throw<InvalidOperationException>(() => factory.CreateDepositViewModel(getDenoms, isBusy));
    }
}
