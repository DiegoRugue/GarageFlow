namespace GarageFlow.Application.Vehicles.VehicleColors.GetVehicleColorById;

public sealed record VehicleColorDto(
    Guid Id,
    string Name,
    DateTime CreatedAt);
