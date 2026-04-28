namespace GarageFlow.Application.WorkOrders;

public sealed record CustomerWorkOrderDetailsDto(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CustomerWorkOrderEstimateDto> Estimates);
