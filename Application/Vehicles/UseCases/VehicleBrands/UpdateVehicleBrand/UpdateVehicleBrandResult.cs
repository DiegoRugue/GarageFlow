namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.UpdateVehicleBrand;

public sealed record UpdateVehicleBrandResult(
    Guid Id,
    string Name,
    DateTime CreatedAt);
