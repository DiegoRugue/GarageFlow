namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.CreateVehicleModel;

public sealed record CreateVehicleModelResult(
    Guid Id,
    Guid VehicleBrandId,
    string Name,
    DateTime CreatedAt);
