namespace GarageFlow.Application.Vehicles.VehicleBrands.UpdateVehicleBrand;

public sealed record UpdateVehicleBrandResult(
    Guid Id,
    string Name,
    DateTime CreatedAt);
