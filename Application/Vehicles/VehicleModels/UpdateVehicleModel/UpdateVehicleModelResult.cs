namespace GarageFlow.Application.Vehicles.VehicleModels.UpdateVehicleModel;

public sealed record UpdateVehicleModelResult(
    Guid Id,
    Guid VehicleBrandId,
    string Name,
    DateTime CreatedAt);
