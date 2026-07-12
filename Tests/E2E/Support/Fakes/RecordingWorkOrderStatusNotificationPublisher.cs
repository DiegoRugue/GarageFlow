using System.Collections.Concurrent;
using System.Threading.Channels;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Application.WorkOrders.Ports;

namespace GarageFlow.Tests.E2E.Support.Fakes;

public sealed class RecordingWorkOrderStatusNotificationPublisher : IWorkOrderStatusNotificationPublisher
{
    private readonly Channel<WorkOrderStatusChangedIntegrationEvent> _published =
        Channel.CreateUnbounded<WorkOrderStatusChangedIntegrationEvent>();
    private readonly ConcurrentQueue<WorkOrderStatusChangedIntegrationEvent> _attempts = new();
    private readonly object _gateLock = new();
    private GateState? _armedGate;
    private GateState? _activeGate;
    private int _failuresRemaining;

    public IReadOnlyCollection<WorkOrderStatusChangedIntegrationEvent> Attempts => _attempts.ToArray();

    public async Task PublishAsync(
        WorkOrderStatusChangedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        _attempts.Enqueue(notification);
        GateState? gate;
        lock (_gateLock)
        {
            gate = _armedGate;
            _armedGate = null;
            _activeGate = gate;
        }

        try
        {
            if (gate is not null)
            {
                gate.Started.TrySetResult();
                await gate.Release.Task.WaitAsync(cancellationToken);
            }

            if (Interlocked.Decrement(ref _failuresRemaining) >= 0)
            {
                throw new InvalidOperationException("Injected publisher failure.");
            }

            await _published.Writer.WriteAsync(notification, cancellationToken);
        }
        finally
        {
            if (gate is not null)
            {
                lock (_gateLock)
                {
                    if (ReferenceEquals(_activeGate, gate))
                    {
                        _activeGate = null;
                    }
                }

                gate.Completion.TrySetResult();
            }
        }
    }

    public void BlockNextAttempt()
    {
        lock (_gateLock)
        {
            if (_armedGate is not null || _activeGate is not null)
            {
                throw new InvalidOperationException("A publisher attempt gate is already armed or active.");
            }

            _armedGate = new GateState();
        }
    }

    public Task WaitForBlockedAttemptAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        lock (_gateLock)
        {
            var gate = _armedGate ?? _activeGate
                ?? throw new InvalidOperationException("No publisher attempt gate is armed or active.");
            return gate.Started.Task.WaitAsync(timeout, cancellationToken);
        }
    }

    public void ReleaseBlockedAttempt()
    {
        lock (_gateLock)
        {
            var gate = _activeGate ?? _armedGate
                ?? throw new InvalidOperationException("No publisher attempt gate is armed or active.");
            gate.Release.TrySetResult();
        }
    }

    public async Task ResetAsync(int failures = 0)
    {
        GateState? activeGate;
        lock (_gateLock)
        {
            _armedGate?.Release.TrySetResult();
            _activeGate?.Release.TrySetResult();
            activeGate = _activeGate;
            _armedGate = null;
        }

        if (activeGate is not null)
        {
            await activeGate.Completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Interlocked.Exchange(ref _failuresRemaining, failures);
        while (_attempts.TryDequeue(out _))
        {
        }

        while (_published.Reader.TryRead(out _))
        {
        }
    }

    public async Task<WorkOrderStatusChangedIntegrationEvent> WaitForPublishedAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        await _published.Reader.ReadAsync(cancellationToken).AsTask().WaitAsync(timeout, cancellationToken);

    private sealed class GateState
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
