namespace GarageFlow.Application.Vehicles.VehicleColors.UpdateVehicleColor;

public sealed record UpdateVehicleColorResult(
    Guid Id,
    string Name,
    DateTime CreatedAt);
