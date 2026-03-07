using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

public class MediatorRefactoringTests
{
    [Fact]
    public void Mediator_ShouldSupportSkipStateVerificationProperty()
    {
        var so = new Mock<SimulatorCashChanger>(new SimulatorDependencies()).Object;
        var mediator = new UposMediator(so);

        mediator.SkipStateVerification = true;
        mediator.SkipStateVerification.ShouldBeTrue();
    }

    [Fact]
    public void Execute_ShouldUseInternalSkipFlag()
    {
        var so = new Mock<SimulatorCashChanger>(new SimulatorDependencies()).Object;
        var mediator = new UposMediator(so);
        var commandMock = new Mock<IUposCommand>();

        mediator.SkipStateVerification = true;
        
        mediator.Execute(commandMock.Object);

        commandMock.Verify(c => c.Verify(mediator), Times.Once);
        commandMock.Verify(c => c.Execute(), Times.Once);
    }
}
