namespace GarageFlow.Tests.Integration.Api.WorkOrders.Contracts;

public sealed record CustomerWorkOrderDetailsResponse(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CustomerWorkOrderEstimateResponse> Estimates);
