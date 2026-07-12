using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.WorkOrders.ReadModels;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Repositories;

public sealed class EfWorkOrderQueries(GarageFlowDbContext dbContext) : IWorkOrderQueries
{
    private readonly GarageFlowDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<WorkOrderDetailsReadModel?> GetDetailsByIdAsync(
        WorkOrderId id,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await CreateWorkOrderDetailsQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return workOrder is null ? null : MapToReadModel(workOrder);
    }

    public async Task<WorkOrderStatusReadModel?> GetStatusByIdAsync(
        WorkOrderId id,
        CancellationToken cancellationToken = default)
    {
        var status = await _dbContext.WorkOrders
            .AsNoTracking()
            .Where(workOrder => workOrder.Id == id)
            .Select(workOrder => new
            {
                Id = workOrder.Id.Value,
                workOrder.Status,
                workOrder.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return status is null
            ? null
            : new WorkOrderStatusReadModel(status.Id, status.Status.ToString(), status.UpdatedAt);
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

    public async Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListActiveDetailsAsync(
        int page,
        int pageSize,
        CustomerId? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = CreateWorkOrderDetailsQuery()
            .Where(workOrder =>
                workOrder.Status == WorkOrderStatus.InProgress ||
                workOrder.Status == WorkOrderStatus.WaitingApproval ||
                workOrder.Status == WorkOrderStatus.Diagnosing ||
                workOrder.Status == WorkOrderStatus.Received);

        if (customerId is not null)
        {
            query = query.Where(workOrder => workOrder.CustomerId == customerId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(workOrder => workOrder.Status == WorkOrderStatus.InProgress ? 0
                : workOrder.Status == WorkOrderStatus.WaitingApproval ? 1
                : workOrder.Status == WorkOrderStatus.Diagnosing ? 2
                : 3)
            .ThenBy(workOrder => workOrder.CreatedAt)
            .ThenBy(workOrder => workOrder.Id.Value)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.Select(MapToReadModel).ToList(), totalCount);
    }

    public async Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListCustomerDetailsAsync(
        int page,
        int pageSize,
        CustomerId customerId,
        CancellationToken cancellationToken = default)
    {
        var query = CreateWorkOrderDetailsQuery()
            .Where(workOrder => workOrder.CustomerId == customerId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(workOrder => workOrder.CreatedAt)
            .ThenBy(workOrder => workOrder.Id.Value)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.Select(MapToReadModel).ToList(), totalCount);
    }

    public async Task<AverageServiceTimeReadModel> GetAverageServiceTimeAsync(
        DateTime completedFrom,
        DateTime completedTo,
        ServiceId? serviceId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<EstimateServiceLine>()
            .AsNoTracking()
            .Where(line =>
                line.Status == EstimateServiceLineStatus.Completed &&
                line.StartedAt.HasValue &&
                line.CompletedAt.HasValue &&
                line.CompletedAt.Value >= completedFrom &&
                line.CompletedAt.Value <= completedTo);

        if (serviceId is not null)
        {
            query = query.Where(line => line.ServiceId == serviceId);
        }

        var completedServicesCount = await query.CountAsync(cancellationToken);
        if (completedServicesCount == 0)
        {
            return new AverageServiceTimeReadModel(
                CompletedServicesCount: 0,
                AverageDurationMinutes: null);
        }

        var averageDurationMinutes = await query.AverageAsync(
            line => (line.CompletedAt!.Value - line.StartedAt!.Value).TotalMinutes,
            cancellationToken);

        return new AverageServiceTimeReadModel(
            CompletedServicesCount: completedServicesCount,
            AverageDurationMinutes: averageDurationMinutes);
    }

    private IQueryable<WorkOrder> CreateWorkOrderDetailsQuery()
    {
        return _dbContext.WorkOrders
            .Include(workOrder => workOrder.Estimates)
            .ThenInclude(estimate => estimate.InventoryLines)
            .Include(workOrder => workOrder.Estimates)
            .ThenInclude(estimate => estimate.ServiceLines)
            .AsSplitQuery()
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
                        line.TotalPrice.Value,
                        line.Status.ToString(),
                        line.StartedAt,
                        line.CompletedAt))
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
