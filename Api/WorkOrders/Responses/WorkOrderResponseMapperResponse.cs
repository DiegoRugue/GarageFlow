using GarageFlow.Application.WorkOrders;
using GarageFlow.Application.WorkOrders.GetWorkOrderById;

namespace GarageFlow.Api.WorkOrders.Responses;

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
        return CreateEstimateResponse(
            GetEstimateShape(estimate),
            estimate.InventoryLines.Select(MapInventoryLine).ToList(),
            estimate.ServiceLines.Select(MapServiceLine).ToList(),
            static (id, workOrderId, status, totalAmount, createdAt, updatedAt, inventoryLines, serviceLines) =>
                new WorkOrderEstimateResponse(id, workOrderId, status, totalAmount, createdAt, updatedAt, inventoryLines, serviceLines));
    }

    private static CustomerWorkOrderEstimateResponse MapCustomerEstimate(CustomerWorkOrderEstimateDto estimate)
    {
        return CreateEstimateResponse(
            GetEstimateShape(estimate),
            estimate.InventoryLines.Select(MapCustomerInventoryLine).ToList(),
            estimate.ServiceLines.Select(MapCustomerServiceLine).ToList(),
            static (id, workOrderId, status, totalAmount, createdAt, updatedAt, inventoryLines, serviceLines) =>
                new CustomerWorkOrderEstimateResponse(id, workOrderId, status, totalAmount, createdAt, updatedAt, inventoryLines, serviceLines));
    }

    private static TResponse CreateEstimateResponse<TInventoryLine, TServiceLine, TResponse>(
        EstimateShape estimate,
        IReadOnlyList<TInventoryLine> inventoryLines,
        IReadOnlyList<TServiceLine> serviceLines,
        Func<Guid, Guid, string, decimal, DateTime, DateTime, IReadOnlyList<TInventoryLine>, IReadOnlyList<TServiceLine>, TResponse> createResponse)
    {
        return createResponse(
            estimate.Id,
            estimate.WorkOrderId,
            estimate.Status,
            estimate.TotalAmount,
            estimate.CreatedAt,
            estimate.UpdatedAt,
            inventoryLines,
            serviceLines);
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
            line.TotalPrice);
    }

    private static CustomerWorkOrderServiceLineResponse MapCustomerServiceLine(CustomerWorkOrderServiceLineDto line)
    {
        return new CustomerWorkOrderServiceLineResponse(
            line.Id,
            line.EstimateId,
            line.ServiceId,
            line.Description,
            line.UnitPrice,
            line.TotalPrice);
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
