namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxMessageEntity
{
    private IntegrationOutboxMessageEntity()
    {
    }

    private IntegrationOutboxMessageEntity(
        Guid id,
        string eventKey,
        Guid aggregateId,
        string payload,
        DateTime occurredAt,
        string? correlationId)
    {
        Id = id;
        EventKey = eventKey;
        AggregateId = aggregateId;
        Payload = payload;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        NextAttemptAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public string EventKey { get; private set; } = null!;
    public Guid AggregateId { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTime OccurredAt { get; private set; }
    public string? CorrelationId { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? LastError { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }

    public static IntegrationOutboxMessageEntity Create(
        Guid id,
        string eventKey,
        Guid aggregateId,
        string payload,
        DateTime occurredAt,
        string? correlationId) =>
        new(id, eventKey, aggregateId, payload, occurredAt, correlationId);
}
