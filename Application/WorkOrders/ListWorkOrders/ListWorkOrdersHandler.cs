using GarageFlow.Application.WorkOrders.GetWorkOrderById;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.WorkOrders.Repositories;
using Mediator;

namespace GarageFlow.Application.WorkOrders.ListWorkOrders;

public sealed class ListWorkOrdersHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<ListWorkOrdersQuery, ListWorkOrdersResult>
{
    private const int MaxPageSize = 100;

    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<ListWorkOrdersResult> Handle(ListWorkOrdersQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1)
        {
            throw new ValidationException($"Page must be greater than or equal to 1. Received: {request.Page}.");
        }

        if (request.PageSize < 1)
        {
            throw new ValidationException($"PageSize must be greater than or equal to 1. Received: {request.PageSize}.");
        }

        if (request.PageSize > MaxPageSize)
        {
            throw new ValidationException($"PageSize cannot exceed {MaxPageSize}. Received: {request.PageSize}.");
        }

        CustomerId? customerId = request.CustomerId is null ? null : CustomerId.From(request.CustomerId.Value);
        var (items, totalCount) = await _workOrderRepository.ListDetailsAsync(
            request.Page,
            request.PageSize,
            customerId,
            cancellationToken);

        return new ListWorkOrdersResult(
            Items: items.Select(MapDetails).ToList(),
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }

    private static WorkOrderDetailsDto MapDetails(WorkOrderDetailsReadModel workOrder)
    {
        return new WorkOrderDetailsDto(
            Id: workOrder.Id,
            CustomerId: workOrder.CustomerId,
            VehicleId: workOrder.VehicleId,
            Status: workOrder.Status,
            CreatedAt: workOrder.CreatedAt,
            UpdatedAt: workOrder.UpdatedAt,
            Estimates: workOrder.Estimates.Select(MapEstimate).ToList());
    }

    private static WorkOrderEstimateDto MapEstimate(WorkOrderEstimateReadModel estimate)
    {
        return new WorkOrderEstimateDto(
            Id: estimate.Id,
            WorkOrderId: estimate.WorkOrderId,
            Status: estimate.Status,
            TotalAmount: estimate.TotalAmount,
            CreatedAt: estimate.CreatedAt,
            UpdatedAt: estimate.UpdatedAt,
            InventoryLines: estimate.InventoryLines.Select(MapInventoryLine).ToList(),
            ServiceLines: estimate.ServiceLines.Select(MapServiceLine).ToList());
    }

    private static WorkOrderInventoryLineDto MapInventoryLine(WorkOrderInventoryLineReadModel line)
    {
        return new WorkOrderInventoryLineDto(
            Id: line.Id,
            EstimateId: line.EstimateId,
            InventoryItemId: line.InventoryItemId,
            Description: line.DescriptionSnapshot,
            Quantity: line.Quantity,
            UnitCost: line.UnitCost,
            UnitPrice: line.UnitPrice,
            TotalPrice: line.TotalPrice);
    }

    private static WorkOrderServiceLineDto MapServiceLine(WorkOrderServiceLineReadModel line)
    {
        return new WorkOrderServiceLineDto(
            Id: line.Id,
            EstimateId: line.EstimateId,
            ServiceId: line.ServiceId,
            Description: line.DescriptionSnapshot,
            UnitPrice: line.UnitPrice,
            TotalPrice: line.TotalPrice);
    }
}
