using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Repositories;

public sealed class WorkOrderRepository(GarageFlowDbContext dbContext) : IWorkOrderRepository
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<WorkOrder?> GetByIdAsync(WorkOrderId id, CancellationToken cancellationToken = default)
    {
        return await CreateWorkOrderAggregateQuery()
            .FirstOrDefaultAsync(workOrder => workOrder.Id == id, cancellationToken);
    }

    public async Task<WorkOrder?> GetByIdForEstimateMutationAsync(WorkOrderId id, CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational() || !_dbContext.Database.IsNpgsql())
        {
            return await GetByIdAsync(id, cancellationToken);
        }

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM "WorkOrders" WHERE "Id" = {id.Value} FOR UPDATE""",
            cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        return _dbContext.WorkOrders.AddAsync(workOrder, cancellationToken).AsTask();
    }

    private IQueryable<WorkOrder> CreateWorkOrderAggregateQuery()
    {
        return _dbContext.WorkOrders
            .Include(workOrder => workOrder.Estimates)
            .ThenInclude(estimate => estimate.InventoryLines)
            .Include(workOrder => workOrder.Estimates)
            .ThenInclude(estimate => estimate.ServiceLines)
            .AsSplitQuery();
    }
}
