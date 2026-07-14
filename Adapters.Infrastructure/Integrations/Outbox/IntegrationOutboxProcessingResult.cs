namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed record IntegrationOutboxProcessingResult(
    int ClaimedCount,
    int ProcessedCount,
    int RescheduledCount,
    int OwnershipLostCount);
