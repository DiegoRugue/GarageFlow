namespace GarageFlow.Tests.Integration.Api.Vehicles.Contracts;

public sealed record VehicleColorResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt);
