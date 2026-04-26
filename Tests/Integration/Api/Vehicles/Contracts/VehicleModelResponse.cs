namespace GarageFlow.Tests.Integration.Api.Vehicles.Contracts;

public sealed record VehicleModelResponse(
    Guid Id,
    Guid VehicleBrandId,
    string Name,
    DateTime CreatedAt);
