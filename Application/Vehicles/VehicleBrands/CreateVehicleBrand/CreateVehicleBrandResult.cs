namespace GarageFlow.Application.Vehicles.VehicleBrands.CreateVehicleBrand;

public sealed record CreateVehicleBrandResult(
    Guid Id,
    string Name,
    DateTime CreatedAt);
