namespace GarageFlow.Tests.Integration.Observability;

internal sealed class ManualMetricsTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _utcNow = utcNow;
    private long _timestamp;

    public override DateTimeOffset GetUtcNow() { lock (_gate) return _utcNow; }
    public override long GetTimestamp() { lock (_gate) return _timestamp; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            var timer = new ManualTimer(this, callback, state);
            _timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
    }
    public bool HasTimer(TimeSpan delay)
    {
        lock (_gate) return _timers.Any(t => !t.Disposed && t.Due == _utcNow + delay);
    }
    public void Advance(TimeSpan elapsed)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _utcNow += elapsed;
            _timestamp += elapsed.Ticks;
            due = _timers.Where(t => !t.Disposed && t.Due <= _utcNow).ToList();
            foreach (var timer in due)
                timer.Due = timer.Period == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : _utcNow + timer.Period;
        }
        foreach (var timer in due) timer.Callback(timer.State);
    }
    private sealed class ManualTimer(ManualMetricsTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public TimerCallback Callback { get; } = callback;
        public object? State { get; } = state;
        public DateTimeOffset Due { get; set; }
        public TimeSpan Period { get; private set; }
        public bool Disposed { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                if (Disposed) return false;
                Due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : owner._utcNow + dueTime;
                Period = period;
                return true;
            }
        }
        public void Dispose() { lock (owner._gate) Disposed = true; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
