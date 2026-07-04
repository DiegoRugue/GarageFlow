using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderById;
using GarageFlow.Application.WorkOrders.ReadModels;

namespace GarageFlow.Application.WorkOrders.Common;

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
        var shape = GetEstimateShape(estimate);

        return new WorkOrderEstimateDto(
            shape.Id,
            shape.WorkOrderId,
            shape.Status,
            shape.TotalAmount,
            shape.CreatedAt,
            shape.UpdatedAt,
            estimate.InventoryLines.Select(MapInventoryLine).ToList(),
            estimate.ServiceLines.Select(MapServiceLine).ToList());
    }

    private static CustomerWorkOrderEstimateDto MapCustomerEstimate(WorkOrderEstimateReadModel estimate)
    {
        var shape = GetEstimateShape(estimate);

        return new CustomerWorkOrderEstimateDto(
            shape.Id,
            shape.WorkOrderId,
            shape.Status,
            shape.TotalAmount,
            shape.CreatedAt,
            shape.UpdatedAt,
            estimate.InventoryLines.Select(MapCustomerInventoryLine).ToList(),
            estimate.ServiceLines.Select(MapCustomerServiceLine).ToList());
    }

    private static EstimateShape GetEstimateShape(WorkOrderEstimateReadModel estimate) =>
        new(estimate.Id, estimate.WorkOrderId, estimate.Status, estimate.TotalAmount, estimate.CreatedAt, estimate.UpdatedAt);

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
            line.TotalPrice,
            line.Status,
            line.StartedAt,
            line.CompletedAt);
    }

    private static CustomerWorkOrderServiceLineDto MapCustomerServiceLine(WorkOrderServiceLineReadModel line)
    {
        return new CustomerWorkOrderServiceLineDto(
            line.Id,
            line.EstimateId,
            line.ServiceId,
            line.DescriptionSnapshot,
            line.UnitPrice,
            line.TotalPrice,
            line.Status,
            line.StartedAt,
            line.CompletedAt);
    }

    private readonly record struct EstimateShape(
        Guid Id,
        Guid WorkOrderId,
        string Status,
        decimal TotalAmount,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
