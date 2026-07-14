namespace GarageFlow.Application.Common.Integrations;

public static class OutboxRetrySchedule
{
    public static TimeSpan ForAttempt(int attempt, TimeSpan initialDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxDelay, TimeSpan.Zero);

        ArgumentOutOfRangeException.ThrowIfLessThan(maxDelay, initialDelay);

        var delayTicks = initialDelay.Ticks;
        var maxTicks = maxDelay.Ticks;
        for (var currentAttempt = 1; currentAttempt < attempt && delayTicks < maxTicks; currentAttempt++)
        {
            delayTicks = delayTicks > maxTicks / 2
                ? maxTicks
                : Math.Min(delayTicks * 2, maxTicks);
        }

        return TimeSpan.FromTicks(delayTicks);
    }
}
