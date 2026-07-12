using System.Text.Json;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Application.WorkOrders.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GarageFlow.Tests.Integration.Integrations;

public sealed class IntegrationOutboxProcessorTests
{
    private static readonly DateTime Now = new(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessBatchAsync_ShouldPublishKnownEventThenMarkProcessed()
    {
        var integrationEvent = new WorkOrderStatusChangedIntegrationEvent(
            Guid.NewGuid(), "Received", "Diagnosing", Now);
        var claim = CreateClaim(1, JsonSerializer.Serialize(integrationEvent));
        var repository = new RecordingRepository(claim);
        var publisher = new RecordingPublisher();
        var processor = CreateProcessor(repository, publisher);

        var result = await processor.ProcessBatchAsync();

        Assert.Equal(integrationEvent, Assert.Single(publisher.Published));
        Assert.Equal((claim.Id, claim.LeaseId), Assert.Single(repository.Marked));
        Assert.Equal(1, result.ProcessedCount);
        Assert.Equal(0, result.OwnershipLostCount);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldUseClaimedAttemptForPublisherRetryAndBoundError()
    {
        var claim = CreateClaim(3, "{}");
        var repository = new RecordingRepository(claim);
        var publisher = new RecordingPublisher(new InvalidOperationException(new string('x', 2_000)));
        var processor = CreateProcessor(repository, publisher);

        var result = await processor.ProcessBatchAsync();

        var retry = Assert.Single(repository.Rescheduled);
        Assert.Equal(Now.AddSeconds(20), retry.NextAttemptAt);
        Assert.Equal(1_024, retry.Error.Length);
        Assert.Equal(1, result.RescheduledCount);
    }

    [Theory]
    [InlineData("unsupported.v1", "{}", "Unsupported integration event key 'unsupported.v1'.")]
    [InlineData(WorkOrderStatusChangedIntegrationEvent.EventKey, "{", "Malformed integration event payload for 'work-order.status-changed.v1'.")]
    public async Task ProcessBatchAsync_ShouldRescheduleUnsupportedOrMalformedMessages(
        string eventKey,
        string payload,
        string expectedDiagnostic)
    {
        var claim = CreateClaim(1, payload, eventKey);
        var repository = new RecordingRepository(claim);
        var processor = CreateProcessor(repository, new RecordingPublisher());

        var result = await processor.ProcessBatchAsync();

        Assert.Equal(expectedDiagnostic, Assert.Single(repository.Rescheduled).Error);
        Assert.Equal(1, result.RescheduledCount);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldReportOwnershipLossInsteadOfFalseSuccess()
    {
        var repository = new RecordingRepository(CreateClaim(1, "{}")) { UpdateResult = false };
        var processor = CreateProcessor(repository, new RecordingPublisher());

        var result = await processor.ProcessBatchAsync();

        Assert.Equal(0, result.ProcessedCount);
        Assert.Equal(1, result.OwnershipLostCount);
    }

    private static IntegrationOutboxProcessor CreateProcessor(
        IIntegrationOutboxRepository repository,
        IWorkOrderStatusNotificationPublisher publisher) =>
        new(
            repository,
            publisher,
            Options.Create(new IntegrationOutboxOptions { Enabled = true }),
            new FixedTimeProvider(Now),
            NullLogger<IntegrationOutboxProcessor>.Instance);

    private static ClaimedIntegrationOutboxMessage CreateClaim(
        int attemptCount,
        string payload,
        string eventKey = WorkOrderStatusChangedIntegrationEvent.EventKey)
    {
        var aggregateId = Guid.NewGuid();
        if (payload == "{}")
        {
            payload = JsonSerializer.Serialize(new WorkOrderStatusChangedIntegrationEvent(
                aggregateId, "Received", "Diagnosing", Now));
        }

        return new ClaimedIntegrationOutboxMessage(
            Guid.NewGuid(), eventKey, aggregateId, payload, Now, "correlation", attemptCount, Guid.NewGuid(), Now.AddMinutes(5));
    }

    private sealed class RecordingRepository(params ClaimedIntegrationOutboxMessage[] claims)
        : IIntegrationOutboxRepository
    {
        public bool UpdateResult { get; init; } = true;
        public List<(Guid Id, Guid LeaseId)> Marked { get; } = [];
        public List<(Guid Id, Guid LeaseId, DateTime NextAttemptAt, string Error)> Rescheduled { get; } = [];

        public Task<IReadOnlyList<ClaimedIntegrationOutboxMessage>> ClaimBatchAsync(
            Guid leaseId, DateTime now, DateTime leaseExpiresAt, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ClaimedIntegrationOutboxMessage>>(claims);

        public Task<bool> MarkProcessedAsync(Guid id, Guid leaseId, DateTime processedAt, CancellationToken cancellationToken = default)
        {
            Marked.Add((id, leaseId));
            return Task.FromResult(UpdateResult);
        }

        public Task<bool> RescheduleAsync(Guid id, Guid leaseId, DateTime nextAttemptAt, string error, CancellationToken cancellationToken = default)
        {
            Rescheduled.Add((id, leaseId, nextAttemptAt, error));
            return Task.FromResult(UpdateResult);
        }
    }

    private sealed class RecordingPublisher(Exception? exception = null) : IWorkOrderStatusNotificationPublisher
    {
        public List<WorkOrderStatusChangedIntegrationEvent> Published { get; } = [];

        public Task PublishAsync(WorkOrderStatusChangedIntegrationEvent notification, CancellationToken cancellationToken = default)
        {
            Published.Add(notification);
            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
