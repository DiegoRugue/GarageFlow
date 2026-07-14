namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public interface IIntegrationOutboxRepository
{
    Task<IReadOnlyList<ClaimedIntegrationOutboxMessage>> ClaimBatchAsync(
        Guid leaseId,
        DateTime now,
        DateTime leaseExpiresAt,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<bool> MarkProcessedAsync(
        Guid id,
        Guid leaseId,
        DateTime processedAt,
        CancellationToken cancellationToken = default);

    Task<bool> RescheduleAsync(
        Guid id,
        Guid leaseId,
        DateTime nextAttemptAt,
        string errorMessage,
        CancellationToken cancellationToken = default);
}
