namespace GarageFlow.Api.WorkOrders.Responses;

public sealed record WorkOrderDetailsResponse(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkOrderEstimateResponse> Estimates);
