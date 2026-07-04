namespace GarageFlow.Adapters.Api.Vehicles.VehicleModels.CreateVehicleModel;

public sealed record CreateVehicleModelResponse(
    Guid Id,
    Guid VehicleBrandId,
    string Name,
    DateTime CreatedAt);
