using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.Ports;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Inbox;

public sealed class EstimateDecisionInbox(GarageFlowDbContext dbContext) : IEstimateDecisionInbox
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<EstimateDecisionInboxRegistration> RegisterAsync(
        Guid eventId, string payloadHash, DateTime occurredAt, DateTime receivedAt,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            var existing = _dbContext.EstimateDecisionInboxEvents.Local.SingleOrDefault(item => item.EventId == eventId)
                ?? await _dbContext.EstimateDecisionInboxEvents.FirstOrDefaultAsync(item => item.EventId == eventId, cancellationToken);
            if (existing is not null)
            {
                return new EstimateDecisionInboxRegistration(false, existing.PayloadHash);
            }

            _dbContext.EstimateDecisionInboxEvents.Add(
                EstimateDecisionInboxEvent.Create(eventId, payloadHash, occurredAt, receivedAt));
            return new EstimateDecisionInboxRegistration(true, payloadHash);
        }

        var inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "EstimateDecisionInboxEvents"
                ("EventId", "PayloadHash", "OccurredAt", "ReceivedAt")
            VALUES ({eventId}, {payloadHash}, {occurredAt}, {receivedAt})
            ON CONFLICT ("EventId") DO NOTHING
            """, cancellationToken);
        if (inserted == 1)
        {
            return new EstimateDecisionInboxRegistration(true, payloadHash);
        }

        var storedHash = await _dbContext.EstimateDecisionInboxEvents.AsNoTracking()
            .Where(item => item.EventId == eventId)
            .Select(item => item.PayloadHash)
            .SingleAsync(cancellationToken);
        return new EstimateDecisionInboxRegistration(false, storedHash);
    }
}
