namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed record ClaimedIntegrationOutboxMessage(
    Guid Id,
    string EventKey,
    Guid AggregateId,
    string Payload,
    DateTime OccurredAt,
    string? CorrelationId,
    int AttemptCount,
    Guid LeaseId,
    DateTime LeaseExpiresAt);
