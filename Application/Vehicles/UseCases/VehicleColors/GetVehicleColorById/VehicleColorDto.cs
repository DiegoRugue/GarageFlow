namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.GetVehicleColorById;

public sealed record VehicleColorDto(
    Guid Id,
    string Name,
    DateTime CreatedAt);
