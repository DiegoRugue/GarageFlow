using GarageFlow.Application.WorkOrders.GetWorkOrderById;
using GarageFlow.Domain.WorkOrders.Repositories;

namespace GarageFlow.Application.WorkOrders;

public sealed record CustomerWorkOrderDetailsDto(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CustomerWorkOrderEstimateDto> Estimates);

internal static class WorkOrderDetailsMapper
{
    public static WorkOrderDetailsDto MapDetails(WorkOrderDetailsReadModel workOrder)
    {
        return MapDetails(
            workOrder,
            MapEstimate,
            static (id, customerId, vehicleId, status, createdAt, updatedAt, estimates) =>
                new WorkOrderDetailsDto(id, customerId, vehicleId, status, createdAt, updatedAt, estimates));
    }

    public static CustomerWorkOrderDetailsDto MapCustomerDetails(WorkOrderDetailsReadModel workOrder)
    {
        return MapDetails(
            workOrder,
            MapCustomerEstimate,
            static (id, customerId, vehicleId, status, createdAt, updatedAt, estimates) =>
                new CustomerWorkOrderDetailsDto(id, customerId, vehicleId, status, createdAt, updatedAt, estimates));
    }

    private static TDetails MapDetails<TDetails, TEstimate>(
        WorkOrderDetailsReadModel workOrder,
        Func<WorkOrderEstimateReadModel, TEstimate> mapEstimate,
        Func<Guid, Guid, Guid, string, DateTime, DateTime, IReadOnlyList<TEstimate>, TDetails> createDetails)
    {
        return createDetails(
            workOrder.Id,
            workOrder.CustomerId,
            workOrder.VehicleId,
            workOrder.Status,
            workOrder.CreatedAt,
            workOrder.UpdatedAt,
            workOrder.Estimates.Select(mapEstimate).ToList());
    }

    private static WorkOrderEstimateDto MapEstimate(WorkOrderEstimateReadModel estimate)
    {
        return MapEstimate(
            estimate,
            MapInventoryLine,
            MapServiceLine,
            static (id, workOrderId, status, totalAmount, createdAt, updatedAt, inventoryLines, serviceLines) =>
                new WorkOrderEstimateDto(id, workOrderId, status, totalAmount, createdAt, updatedAt, inventoryLines, serviceLines));
    }

    private static CustomerWorkOrderEstimateDto MapCustomerEstimate(WorkOrderEstimateReadModel estimate)
    {
        return MapEstimate(
            estimate,
            MapCustomerInventoryLine,
            MapCustomerServiceLine,
            static (id, workOrderId, status, totalAmount, createdAt, updatedAt, inventoryLines, serviceLines) =>
                new CustomerWorkOrderEstimateDto(id, workOrderId, status, totalAmount, createdAt, updatedAt, inventoryLines, serviceLines));
    }

    private static TEstimate MapEstimate<TEstimate, TInventoryLine, TServiceLine>(
        WorkOrderEstimateReadModel estimate,
        Func<WorkOrderInventoryLineReadModel, TInventoryLine> mapInventoryLine,
        Func<WorkOrderServiceLineReadModel, TServiceLine> mapServiceLine,
        Func<Guid, Guid, string, decimal, DateTime, DateTime, IReadOnlyList<TInventoryLine>, IReadOnlyList<TServiceLine>, TEstimate> createEstimate)
    {
        return createEstimate(
            estimate.Id,
            estimate.WorkOrderId,
            estimate.Status,
            estimate.TotalAmount,
            estimate.CreatedAt,
            estimate.UpdatedAt,
            estimate.InventoryLines.Select(mapInventoryLine).ToList(),
            estimate.ServiceLines.Select(mapServiceLine).ToList());
    }

    private static WorkOrderInventoryLineDto MapInventoryLine(WorkOrderInventoryLineReadModel line)
    {
        return new WorkOrderInventoryLineDto(
            line.Id,
            line.EstimateId,
            line.InventoryItemId,
            line.DescriptionSnapshot,
            line.Quantity,
            line.UnitCost,
            line.UnitPrice,
            line.TotalPrice);
    }

    private static CustomerWorkOrderInventoryLineDto MapCustomerInventoryLine(WorkOrderInventoryLineReadModel line)
    {
        return new CustomerWorkOrderInventoryLineDto(
            line.Id,
            line.EstimateId,
            line.InventoryItemId,
            line.DescriptionSnapshot,
            line.Quantity,
            line.UnitPrice,
            line.TotalPrice);
    }

    private static WorkOrderServiceLineDto MapServiceLine(WorkOrderServiceLineReadModel line)
    {
        return new WorkOrderServiceLineDto(
            line.Id,
            line.EstimateId,
            line.ServiceId,
            line.DescriptionSnapshot,
            line.UnitPrice,
            line.TotalPrice);
    }

    private static CustomerWorkOrderServiceLineDto MapCustomerServiceLine(WorkOrderServiceLineReadModel line)
    {
        return new CustomerWorkOrderServiceLineDto(
            line.Id,
            line.EstimateId,
            line.ServiceId,
            line.DescriptionSnapshot,
            line.UnitPrice,
            line.TotalPrice);
    }
}
