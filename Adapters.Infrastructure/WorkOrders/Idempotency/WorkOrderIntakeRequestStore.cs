using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Application.WorkOrders.Ports;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Idempotency;

public sealed class WorkOrderIntakeRequestStore(GarageFlowDbContext dbContext) : IWorkOrderIntakeRequestStore
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<IntakeRequestClaim> ClaimAsync(
        Guid requestId,
        string payloadHash,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            var existing = _dbContext.WorkOrderIntakeRequests.Local
                .SingleOrDefault(receipt => receipt.RequestId == requestId)
                ?? await _dbContext.WorkOrderIntakeRequests
                    .FirstOrDefaultAsync(receipt => receipt.RequestId == requestId, cancellationToken);
            if (existing is not null)
            {
                return new IntakeRequestClaim(
                    IntakeRequestClaimState.Completed,
                    existing.PayloadHash,
                    existing.ResponseJson);
            }

            _dbContext.WorkOrderIntakeRequests.Add(
                IntakeRequestReceipt.Create(requestId, payloadHash, DateTime.UtcNow));
            return new IntakeRequestClaim(IntakeRequestClaimState.Acquired, payloadHash, null);
        }

        var inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "WorkOrderIntakeRequests" ("RequestId", "PayloadHash", "CreatedAt")
            VALUES ({requestId}, {payloadHash}, CURRENT_TIMESTAMP)
            ON CONFLICT ("RequestId") DO NOTHING
            """,
            cancellationToken);
        if (inserted == 1)
        {
            return new IntakeRequestClaim(IntakeRequestClaimState.Acquired, payloadHash, null);
        }

        var receipt = await _dbContext.WorkOrderIntakeRequests
            .AsNoTracking()
            .SingleAsync(candidate => candidate.RequestId == requestId, cancellationToken);
        return new IntakeRequestClaim(
            IntakeRequestClaimState.Completed,
            receipt.PayloadHash,
            receipt.ResponseJson);
    }

    public async Task CompleteAsync(
        Guid requestId,
        Guid workOrderId,
        string responseJson,
        DateTime completedAt,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsNpgsql())
        {
            var receipt = _dbContext.WorkOrderIntakeRequests.Local
                .SingleOrDefault(candidate => candidate.RequestId == requestId)
                ?? await _dbContext.WorkOrderIntakeRequests
                    .SingleOrDefaultAsync(candidate => candidate.RequestId == requestId, cancellationToken)
                ?? throw new InvalidOperationException("The intake request receipt was not found.");
            receipt.Complete(workOrderId, responseJson, completedAt);
            return;
        }

        var affectedRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "WorkOrderIntakeRequests"
            SET "WorkOrderId" = {workOrderId},
                "ResponseJson" = CAST({responseJson} AS jsonb),
                "CompletedAt" = {completedAt}
            WHERE "RequestId" = {requestId}
            """,
            cancellationToken);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException("Exactly one intake request receipt must be completed.");
        }
    }
}
