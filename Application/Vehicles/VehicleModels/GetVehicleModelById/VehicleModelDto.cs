namespace GarageFlow.Application.Vehicles.VehicleModels.GetVehicleModelById;

public sealed record VehicleModelDto(
    Guid Id,
    Guid VehicleBrandId,
    string Name,
    DateTime CreatedAt);
