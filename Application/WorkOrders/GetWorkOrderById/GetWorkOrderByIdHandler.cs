using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.GetWorkOrderById;

public sealed class GetWorkOrderByIdHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<GetWorkOrderByIdQuery, WorkOrderDetailsDto>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<WorkOrderDetailsDto> Handle(GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.Id);
        var workOrder = await _workOrderRepository.GetDetailsByIdAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.Id}' was not found.");
        }

        return MapDetails(workOrder);
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
