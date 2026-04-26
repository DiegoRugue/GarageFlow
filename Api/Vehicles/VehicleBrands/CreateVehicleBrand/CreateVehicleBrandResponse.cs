namespace GarageFlow.Api.Vehicles.VehicleBrands.CreateVehicleBrand;

public sealed record CreateVehicleBrandResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt);
