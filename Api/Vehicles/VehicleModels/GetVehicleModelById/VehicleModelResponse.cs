namespace GarageFlow.Api.Vehicles.VehicleModels.GetVehicleModelById;

public sealed record VehicleModelResponse(
    Guid Id,
    Guid VehicleBrandId,
    string Name,
    DateTime CreatedAt);
