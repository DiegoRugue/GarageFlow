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
    private int _failuresRemaining;

    public IReadOnlyCollection<WorkOrderStatusChangedIntegrationEvent> Attempts => _attempts.ToArray();

    public Task PublishAsync(
        WorkOrderStatusChangedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        _attempts.Enqueue(notification);
        if (Interlocked.Decrement(ref _failuresRemaining) >= 0)
        {
            throw new InvalidOperationException("Injected publisher failure.");
        }

        return _published.Writer.WriteAsync(notification, cancellationToken).AsTask();
    }

    public void Reset(int failures = 0)
    {
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
}
