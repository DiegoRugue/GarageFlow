namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.UpdateVehicleColor;

public sealed record UpdateVehicleColorResult(
    Guid Id,
    string Name,
    DateTime CreatedAt);
