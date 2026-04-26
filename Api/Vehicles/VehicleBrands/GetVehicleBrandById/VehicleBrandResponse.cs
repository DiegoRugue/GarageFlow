namespace GarageFlow.Api.Vehicles.VehicleBrands.GetVehicleBrandById;

public sealed record VehicleBrandResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt);
