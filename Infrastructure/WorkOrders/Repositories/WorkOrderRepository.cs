using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Infrastructure.WorkOrders.Repositories;

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

    public async Task<WorkOrderDetailsReadModel?> GetDetailsByIdAsync(
        WorkOrderId id,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await CreateWorkOrderDetailsQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return workOrder is null ? null : MapToReadModel(workOrder);
    }

    public async Task<WorkOrderDetailsReadModel?> GetCustomerDetailsByIdAsync(
        WorkOrderId id,
        CustomerId customerId,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await CreateWorkOrderDetailsQuery()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id && candidate.CustomerId == customerId,
                cancellationToken);

        return workOrder is null ? null : MapToReadModel(workOrder);
    }

    public async Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CustomerId? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = CreateWorkOrderDetailsQuery();

        if (customerId is not null)
        {
            query = query.Where(workOrder => workOrder.CustomerId == customerId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(workOrder => workOrder.CreatedAt)
            .ThenBy(workOrder => workOrder.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.Select(MapToReadModel).ToList(), totalCount);
    }

    public Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListCustomerDetailsAsync(
        int page,
        int pageSize,
        CustomerId customerId,
        CancellationToken cancellationToken = default)
    {
        return ListDetailsAsync(page, pageSize, customerId, cancellationToken);
    }

    public async Task<AverageServiceTimeReadModel> GetAverageServiceTimeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var durations = await _dbContext.WorkOrders
            .AsNoTracking()
            .Where(workOrder =>
                workOrder.StartedAt.HasValue &&
                workOrder.CompletedAt.HasValue &&
                workOrder.CompletedAt.Value >= from &&
                workOrder.CompletedAt.Value <= to)
            .Select(workOrder => new
            {
                StartedAt = workOrder.StartedAt!.Value,
                CompletedAt = workOrder.CompletedAt!.Value
            })
            .ToListAsync(cancellationToken);

        if (durations.Count == 0)
        {
            return new AverageServiceTimeReadModel(
                CompletedWorkOrdersCount: 0,
                AverageDurationMinutes: null);
        }

        var averageDurationMinutes = durations.Average(duration =>
            (duration.CompletedAt - duration.StartedAt).TotalMinutes);

        return new AverageServiceTimeReadModel(
            CompletedWorkOrdersCount: durations.Count,
            AverageDurationMinutes: averageDurationMinutes);
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

    private IQueryable<WorkOrder> CreateWorkOrderDetailsQuery()
    {
        return CreateWorkOrderAggregateQuery()
            .AsNoTracking();
    }

    private static WorkOrderDetailsReadModel MapToReadModel(WorkOrder workOrder)
    {
        var estimates = workOrder.Estimates
            .OrderBy(estimate => estimate.CreatedAt)
            .ThenBy(estimate => estimate.Id.Value)
            .Select(estimate =>
            {
                var inventoryLines = estimate.InventoryLines
                    .OrderBy(line => line.CreatedAt)
                    .ThenBy(line => line.Id.Value)
                    .Select(line => new WorkOrderInventoryLineReadModel(
                        line.Id.Value,
                        line.EstimateId.Value,
                        line.InventoryItemId.Value,
                        line.DescriptionSnapshot.Value,
                        line.Quantity.Value,
                        line.UnitCost.Value,
                        line.UnitPrice.Value,
                        line.TotalPrice.Value))
                    .ToList();

                var serviceLines = estimate.ServiceLines
                    .OrderBy(line => line.CreatedAt)
                    .ThenBy(line => line.Id.Value)
                    .Select(line => new WorkOrderServiceLineReadModel(
                        line.Id.Value,
                        line.EstimateId.Value,
                        line.ServiceId.Value,
                        line.DescriptionSnapshot.Value,
                        line.UnitPrice.Value,
                        line.TotalPrice.Value))
                    .ToList();

                return new WorkOrderEstimateReadModel(
                    estimate.Id.Value,
                    estimate.WorkOrderId.Value,
                    estimate.Status.ToString(),
                    estimate.TotalAmount.Value,
                    estimate.CreatedAt,
                    estimate.UpdatedAt,
                    inventoryLines,
                    serviceLines);
            })
            .ToList();

        return new WorkOrderDetailsReadModel(
            workOrder.Id.Value,
            workOrder.CustomerId.Value,
            workOrder.VehicleId.Value,
            workOrder.Status.ToString(),
            workOrder.CreatedAt,
            workOrder.UpdatedAt,
            estimates);
    }
}
