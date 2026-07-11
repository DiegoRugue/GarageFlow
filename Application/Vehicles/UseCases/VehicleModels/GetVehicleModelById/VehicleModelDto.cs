namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.GetVehicleModelById;

public sealed record VehicleModelDto(
    Guid Id,
    Guid VehicleBrandId,
    string Name,
    DateTime CreatedAt);
