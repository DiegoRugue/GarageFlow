namespace GarageFlow.Application.WorkOrders.GetWorkOrderById;

public sealed record WorkOrderDetailsDto(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkOrderEstimateDto> Estimates);
