namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.CreateVehicleBrand;

public sealed record CreateVehicleBrandResult(
    Guid Id,
    string Name,
    DateTime CreatedAt);
