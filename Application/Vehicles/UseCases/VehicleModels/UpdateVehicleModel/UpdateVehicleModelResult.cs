namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.UpdateVehicleModel;

public sealed record UpdateVehicleModelResult(
    Guid Id,
    Guid VehicleBrandId,
    string Name,
    DateTime CreatedAt);
