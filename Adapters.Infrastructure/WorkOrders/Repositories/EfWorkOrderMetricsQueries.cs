using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.WorkOrders.ReadModels;
using GarageFlow.Domain.WorkOrders.Enums;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Repositories;

public sealed class EfWorkOrderMetricsQueries(GarageFlowDbContext dbContext) : IWorkOrderMetricsQueries
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<WorkOrderDailyMetricsReadModel> GetDailyAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var workOrders = _dbContext.WorkOrders.AsNoTracking();
        var createdCount = await workOrders.LongCountAsync(
            order => order.CreatedAt >= fromUtc && order.CreatedAt < toUtc,
            cancellationToken);

        var completed = await workOrders
            .Where(order =>
                (order.Status == WorkOrderStatus.Completed || order.Status == WorkOrderStatus.Delivered) &&
                order.StartedAt.HasValue && order.CompletedAt.HasValue &&
                order.CompletedAt.Value >= order.StartedAt.Value &&
                order.CompletedAt.Value >= fromUtc && order.CompletedAt.Value < toUtc)
            .GroupBy(order => 1)
            .Select(group => new
            {
                Count = group.LongCount(),
                AverageDurationSeconds = group.Average(order =>
                    (double?)(order.CompletedAt!.Value - order.StartedAt!.Value).TotalSeconds)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new WorkOrderDailyMetricsReadModel(
            createdCount,
            completed?.Count ?? 0,
            completed?.AverageDurationSeconds);
    }
}
