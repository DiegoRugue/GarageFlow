using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

public sealed class IntegrationOutboxRepository(GarageFlowDbContext dbContext) : IIntegrationOutboxRepository
{
    public const int MaximumErrorLength = 1_024;

    private readonly GarageFlowDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<IReadOnlyList<ClaimedIntegrationOutboxMessage>> ClaimBatchAsync(
        Guid leaseId,
        DateTime now,
        DateTime leaseExpiresAt,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseExpiresAt, now);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var entities = await _dbContext.IntegrationOutboxMessages
            .FromSqlInterpolated($$"""
                SELECT *
                FROM "IntegrationOutboxMessages"
                WHERE "ProcessedAt" IS NULL
                  AND "NextAttemptAt" <= {{now}}
                  AND ("LeaseExpiresAt" IS NULL OR "LeaseExpiresAt" <= {{now}})
                ORDER BY "NextAttemptAt", "OccurredAt", "Id"
                FOR UPDATE SKIP LOCKED
                LIMIT {{batchSize}}
                """)
            .ToListAsync(cancellationToken);

        foreach (var entity in entities)
        {
            entity.AcquireLease(leaseId, leaseExpiresAt);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var claims = entities.Select(entity => new ClaimedIntegrationOutboxMessage(
            entity.Id,
            entity.EventKey,
            entity.AggregateId,
            entity.Payload,
            entity.OccurredAt,
            entity.CorrelationId,
            entity.AttemptCount,
            leaseId,
            leaseExpiresAt)).ToArray();

        foreach (var entity in entities)
        {
            _dbContext.Entry(entity).State = EntityState.Detached;
        }

        return claims;
    }

    public async Task<bool> MarkProcessedAsync(
        Guid id,
        Guid leaseId,
        DateTime processedAt,
        CancellationToken cancellationToken = default)
    {
        var affected = await _dbContext.IntegrationOutboxMessages
            .Where(message => message.Id == id && message.LeaseId == leaseId && message.ProcessedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.ProcessedAt, processedAt)
                .SetProperty(message => message.LeaseId, (Guid?)null)
                .SetProperty(message => message.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(message => message.LastError, (string?)null), cancellationToken);

        return affected == 1;
    }

    public async Task<bool> RescheduleAsync(
        Guid id,
        Guid leaseId,
        DateTime nextAttemptAt,
        string error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);
        var boundedError = error.Length <= MaximumErrorLength
            ? error
            : error[..MaximumErrorLength];
        var affected = await _dbContext.IntegrationOutboxMessages
            .Where(message => message.Id == id && message.LeaseId == leaseId && message.ProcessedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.NextAttemptAt, nextAttemptAt)
                .SetProperty(message => message.LastError, boundedError)
                .SetProperty(message => message.LeaseId, (Guid?)null)
                .SetProperty(message => message.LeaseExpiresAt, (DateTime?)null), cancellationToken);

        return affected == 1;
    }
}
