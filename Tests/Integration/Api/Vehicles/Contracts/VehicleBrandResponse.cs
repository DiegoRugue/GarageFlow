namespace GarageFlow.Tests.Integration.Api.Vehicles.Contracts;

public sealed record VehicleBrandResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt);
