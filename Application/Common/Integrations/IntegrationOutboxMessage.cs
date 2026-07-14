namespace GarageFlow.Application.Common.Integrations;

public sealed record IntegrationOutboxMessage(
    Guid Id,
    string EventKey,
    Guid AggregateId,
    string Payload,
    DateTime OccurredAt,
    string? CorrelationId);
