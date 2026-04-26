namespace GarageFlow.Application.Vehicles.VehicleBrands.GetVehicleBrandById;

public sealed record VehicleBrandDto(
    Guid Id,
    string Name,
    DateTime CreatedAt);
