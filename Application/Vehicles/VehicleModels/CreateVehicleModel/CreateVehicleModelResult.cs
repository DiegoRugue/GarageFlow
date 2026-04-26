namespace GarageFlow.Application.Vehicles.VehicleModels.CreateVehicleModel;

public sealed record CreateVehicleModelResult(
    Guid Id,
    Guid VehicleBrandId,
    string Name,
    DateTime CreatedAt);
