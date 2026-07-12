using System.Text.Json;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.Events;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GarageFlow.Tests.Integration.Integrations;

public sealed class IntegrationOutboxProcessorTests
{
    private static readonly DateTime Now = new(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessBatchAsync_ShouldPublishKnownEventThenMarkProcessed()
    {
        var workOrderId = Guid.NewGuid();
        var mapped = Assert.Single(new WorkOrderIntegrationOutboxMapper().Map(
            [new WorkOrderStatusChanged(
                WorkOrderId.From(workOrderId),
                WorkOrderStatus.Received,
                WorkOrderStatus.Diagnosing,
                Now)],
            "correlation"));
        var claim = new ClaimedIntegrationOutboxMessage(
            mapped.Id,
            mapped.EventKey,
            mapped.AggregateId,
            mapped.Payload,
            mapped.OccurredAt,
            mapped.CorrelationId,
            1,
            Guid.NewGuid(),
            Now.AddMinutes(5));
        var repository = new RecordingRepository(claim);
        var publisher = new RecordingPublisher();
        var processor = CreateProcessor(repository, publisher);

        var result = await processor.ProcessBatchAsync();

        Assert.Equal(
            new WorkOrderStatusChangedIntegrationEvent(
                workOrderId,
                nameof(WorkOrderStatus.Received),
                nameof(WorkOrderStatus.Diagnosing),
                Now),
            Assert.Single(publisher.Published));
        Assert.Equal((claim.Id, claim.LeaseId, Now), Assert.Single(repository.Marked));
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

    [Fact]
    public async Task ProcessBatchAsync_ShouldRescheduleSemanticallyInvalidKnownEvent()
    {
        var aggregateId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new WorkOrderStatusChangedIntegrationEvent(
            Guid.Empty,
            "",
            "Diagnosing",
            DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        var claim = CreateClaim(1, payload, aggregateId: aggregateId);
        var repository = new RecordingRepository(claim);
        var publisher = new RecordingPublisher();
        var processor = CreateProcessor(repository, publisher);

        var result = await processor.ProcessBatchAsync();

        Assert.Empty(publisher.Published);
        Assert.Equal(
            "Invalid integration event payload for 'work-order.status-changed.v1'.",
            Assert.Single(repository.Rescheduled).Error);
        Assert.Equal(1, result.RescheduledCount);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldMarkProcessedAtPublishCompletionTime()
    {
        var clock = new AdjustableTimeProvider(Now);
        var workOrderId = Guid.NewGuid();
        var claim = CreateClaim(1, ValidPayload(workOrderId), aggregateId: workOrderId);
        var repository = new RecordingRepository(claim);
        var publisher = new RecordingPublisher(onPublish: () => clock.Advance(TimeSpan.FromSeconds(8)));
        var processor = CreateProcessor(repository, publisher, clock);

        await processor.ProcessBatchAsync();

        Assert.Equal(Now.AddSeconds(8), Assert.Single(repository.Marked).ProcessedAt);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldScheduleRetryFromPublishFailureTime()
    {
        var clock = new AdjustableTimeProvider(Now);
        var workOrderId = Guid.NewGuid();
        var claim = CreateClaim(1, ValidPayload(workOrderId), aggregateId: workOrderId);
        var repository = new RecordingRepository(claim);
        var publisher = new RecordingPublisher(
            new InvalidOperationException("publish failed"),
            () => clock.Advance(TimeSpan.FromSeconds(8)));
        var processor = CreateProcessor(repository, publisher, clock);

        await processor.ProcessBatchAsync();

        Assert.Equal(Now.AddSeconds(13), Assert.Single(repository.Rescheduled).NextAttemptAt);
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
        IWorkOrderStatusNotificationPublisher publisher,
        TimeProvider? timeProvider = null) =>
        new(
            repository,
            publisher,
            Options.Create(new IntegrationOutboxOptions { Enabled = true }),
            timeProvider ?? new FixedTimeProvider(Now),
            NullLogger<IntegrationOutboxProcessor>.Instance);

    private static ClaimedIntegrationOutboxMessage CreateClaim(
        int attemptCount,
        string payload,
        string eventKey = WorkOrderStatusChangedIntegrationEvent.EventKey,
        Guid? aggregateId = null)
    {
        var resolvedAggregateId = aggregateId ?? Guid.NewGuid();
        if (payload == "{}")
        {
            payload = JsonSerializer.Serialize(new WorkOrderStatusChangedIntegrationEvent(
                resolvedAggregateId, "Received", "Diagnosing", Now));
        }

        return new ClaimedIntegrationOutboxMessage(
            Guid.NewGuid(), eventKey, resolvedAggregateId, payload, Now, "correlation", attemptCount, Guid.NewGuid(), Now.AddMinutes(5));
    }

    private static string ValidPayload(Guid? workOrderId = null) =>
        JsonSerializer.Serialize(new WorkOrderStatusChangedIntegrationEvent(
            workOrderId ?? Guid.NewGuid(), "Received", "Diagnosing", Now));

    private sealed class RecordingRepository(params ClaimedIntegrationOutboxMessage[] claims)
        : IIntegrationOutboxRepository
    {
        public bool UpdateResult { get; init; } = true;
        public List<(Guid Id, Guid LeaseId, DateTime ProcessedAt)> Marked { get; } = [];
        public List<(Guid Id, Guid LeaseId, DateTime NextAttemptAt, string Error)> Rescheduled { get; } = [];

        public Task<IReadOnlyList<ClaimedIntegrationOutboxMessage>> ClaimBatchAsync(
            Guid leaseId, DateTime now, DateTime leaseExpiresAt, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ClaimedIntegrationOutboxMessage>>(claims);

        public Task<bool> MarkProcessedAsync(Guid id, Guid leaseId, DateTime processedAt, CancellationToken cancellationToken = default)
        {
            Marked.Add((id, leaseId, processedAt));
            return Task.FromResult(UpdateResult);
        }

        public Task<bool> RescheduleAsync(Guid id, Guid leaseId, DateTime nextAttemptAt, string error, CancellationToken cancellationToken = default)
        {
            Rescheduled.Add((id, leaseId, nextAttemptAt, error));
            return Task.FromResult(UpdateResult);
        }
    }

    private sealed class RecordingPublisher(
        Exception? exception = null,
        Action? onPublish = null) : IWorkOrderStatusNotificationPublisher
    {
        public List<WorkOrderStatusChangedIntegrationEvent> Published { get; } = [];

        public Task PublishAsync(WorkOrderStatusChangedIntegrationEvent notification, CancellationToken cancellationToken = default)
        {
            Published.Add(notification);
            onPublish?.Invoke();
            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class AdjustableTimeProvider(DateTime utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = new(utcNow);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan elapsed) => _utcNow = _utcNow.Add(elapsed);
    }
}
