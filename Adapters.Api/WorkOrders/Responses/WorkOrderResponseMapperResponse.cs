using GarageFlow.Application.WorkOrders;
using GarageFlow.Application.WorkOrders.GetWorkOrderById;

namespace GarageFlow.Adapters.Api.WorkOrders.Responses;

internal static class WorkOrderResponseMapper
{
    public static WorkOrderDetailsResponse MapDetails(WorkOrderDetailsDto details)
    {
        return CreateDetailsResponse(
            GetDetailsShape(details),
            details.Estimates.Select(MapEstimate).ToList(),
            static (id, customerId, vehicleId, status, createdAt, updatedAt, estimates) =>
                new WorkOrderDetailsResponse(id, customerId, vehicleId, status, createdAt, updatedAt, estimates));
    }

    public static CustomerWorkOrderDetailsResponse MapDetails(CustomerWorkOrderDetailsDto details)
    {
        return CreateDetailsResponse(
            GetDetailsShape(details),
            details.Estimates.Select(MapCustomerEstimate).ToList(),
            static (id, customerId, vehicleId, status, createdAt, updatedAt, estimates) =>
                new CustomerWorkOrderDetailsResponse(id, customerId, vehicleId, status, createdAt, updatedAt, estimates));
    }

    private static TResponse CreateDetailsResponse<TEstimate, TResponse>(
        DetailsShape details,
        IReadOnlyList<TEstimate> estimates,
        Func<Guid, Guid, Guid, string, DateTime, DateTime, IReadOnlyList<TEstimate>, TResponse> createResponse)
    {
        return createResponse(
            details.Id,
            details.CustomerId,
            details.VehicleId,
            details.Status,
            details.CreatedAt,
            details.UpdatedAt,
            estimates);
    }

    private static DetailsShape GetDetailsShape(WorkOrderDetailsDto details) =>
        new(details.Id, details.CustomerId, details.VehicleId, details.Status, details.CreatedAt, details.UpdatedAt);

    private static DetailsShape GetDetailsShape(CustomerWorkOrderDetailsDto details) =>
        new(details.Id, details.CustomerId, details.VehicleId, details.Status, details.CreatedAt, details.UpdatedAt);

    private static WorkOrderEstimateResponse MapEstimate(WorkOrderEstimateDto estimate)
    {
        var shape = GetEstimateShape(estimate);

        return new WorkOrderEstimateResponse(
            shape.Id,
            shape.WorkOrderId,
            shape.Status,
            shape.TotalAmount,
            shape.CreatedAt,
            shape.UpdatedAt,
            estimate.InventoryLines.Select(MapInventoryLine).ToList(),
            estimate.ServiceLines.Select(MapServiceLine).ToList());
    }

    private static CustomerWorkOrderEstimateResponse MapCustomerEstimate(CustomerWorkOrderEstimateDto estimate)
    {
        var shape = GetEstimateShape(estimate);

        return new CustomerWorkOrderEstimateResponse(
            shape.Id,
            shape.WorkOrderId,
            shape.Status,
            shape.TotalAmount,
            shape.CreatedAt,
            shape.UpdatedAt,
            estimate.InventoryLines.Select(MapCustomerInventoryLine).ToList(),
            estimate.ServiceLines.Select(MapCustomerServiceLine).ToList());
    }

    private static EstimateShape GetEstimateShape(WorkOrderEstimateDto estimate) =>
        new(estimate.Id, estimate.WorkOrderId, estimate.Status, estimate.TotalAmount, estimate.CreatedAt, estimate.UpdatedAt);

    private static EstimateShape GetEstimateShape(CustomerWorkOrderEstimateDto estimate) =>
        new(estimate.Id, estimate.WorkOrderId, estimate.Status, estimate.TotalAmount, estimate.CreatedAt, estimate.UpdatedAt);

    private static WorkOrderInventoryLineResponse MapInventoryLine(WorkOrderInventoryLineDto line)
    {
        return new WorkOrderInventoryLineResponse(
            line.Id,
            line.EstimateId,
            line.InventoryItemId,
            line.Description,
            line.Quantity,
            line.UnitCost,
            line.UnitPrice,
            line.TotalPrice);
    }

    private static CustomerWorkOrderInventoryLineResponse MapCustomerInventoryLine(CustomerWorkOrderInventoryLineDto line)
    {
        return new CustomerWorkOrderInventoryLineResponse(
            line.Id,
            line.EstimateId,
            line.InventoryItemId,
            line.Description,
            line.Quantity,
            line.UnitPrice,
            line.TotalPrice);
    }

    private static WorkOrderServiceLineResponse MapServiceLine(WorkOrderServiceLineDto line)
    {
        return new WorkOrderServiceLineResponse(
            line.Id,
            line.EstimateId,
            line.ServiceId,
            line.Description,
            line.UnitPrice,
            line.TotalPrice,
            line.Status,
            line.StartedAt,
            line.CompletedAt);
    }

    private static CustomerWorkOrderServiceLineResponse MapCustomerServiceLine(CustomerWorkOrderServiceLineDto line)
    {
        return new CustomerWorkOrderServiceLineResponse(
            line.Id,
            line.EstimateId,
            line.ServiceId,
            line.Description,
            line.UnitPrice,
            line.TotalPrice,
            line.Status,
            line.StartedAt,
            line.CompletedAt);
    }

    private readonly record struct DetailsShape(
        Guid Id,
        Guid CustomerId,
        Guid VehicleId,
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private readonly record struct EstimateShape(
        Guid Id,
        Guid WorkOrderId,
        string Status,
        decimal TotalAmount,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
