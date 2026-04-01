using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UposMediator のリファクタリングに関わる機能（検証スキップフラグ等）を検証するテストクラス。</summary>
public class MediatorRefactoringTests
{
    /// <summary>メディエータが検証スキップフラグ（SkipStateVerification）を正しく保持できることを検証します。</summary>
    [Fact]
    public void MediatorShouldSupportSkipStateVerificationProperty()
    {
        var so = new Mock<SimulatorCashChanger>(new SimulatorDependencies()).Object;
        var mediator = new UposMediator(so);

        mediator.SkipStateVerification = true;
        mediator.SkipStateVerification.ShouldBeTrue();
    }

    /// <summary>Execute メソッドが内部のスキップフラグを参照してコマンドを実行することを検証します。</summary>
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
