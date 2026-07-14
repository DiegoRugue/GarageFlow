using GarageFlow.Application.Common.Integrations;

namespace GarageFlow.Tests.Unit.Common;

public sealed class OutboxRetryScheduleTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 80)]
    [InlineData(6, 160)]
    [InlineData(7, 300)]
    [InlineData(8, 300)]
    public void ForAttempt_ShouldExponentiallyBackOffAndCap(int attempt, int expectedSeconds)
    {
        var delay = OutboxRetrySchedule.ForAttempt(
            attempt,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(300));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void ForAttempt_ShouldHonorNonDefaultBoundsAndAvoidOverflow()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(12),
            OutboxRetrySchedule.ForAttempt(3, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(20)));
        Assert.Equal(
            TimeSpan.FromSeconds(20),
            OutboxRetrySchedule.ForAttempt(int.MaxValue, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(20)));
    }

    [Theory]
    [InlineData(0, 5, 300)]
    [InlineData(-1, 5, 300)]
    [InlineData(1, 0, 300)]
    [InlineData(1, -1, 300)]
    [InlineData(1, 5, 0)]
    [InlineData(1, 10, 5)]
    public void ForAttempt_ShouldRejectInvalidArguments(int attempt, int initialSeconds, int maxSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OutboxRetrySchedule.ForAttempt(
            attempt,
            TimeSpan.FromSeconds(initialSeconds),
            TimeSpan.FromSeconds(maxSeconds)));
    }
}
