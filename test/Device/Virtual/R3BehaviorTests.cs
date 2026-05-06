using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device.Virtual;

public class R3BehaviorTests
{
    [Fact]
    public void R3DoesNotUnsubscribeOnException()
    {
        // Arrange
        var subject = new Subject<int>();
        int callCount = 0;
        
        using var sub = subject.Subscribe(x => 
        {
            callCount++;
            if (x == 0) throw new Exception("Test Exception");
        });

        // Act
        try
        {
            subject.OnNext(0);
        }
        catch
        {
            // Ignore
        }

        subject.OnNext(1);

        // Assert
        callCount.ShouldBe(2, "R3 should not unsubscribe even if an exception occurs in the subscriber.");
    }
}
