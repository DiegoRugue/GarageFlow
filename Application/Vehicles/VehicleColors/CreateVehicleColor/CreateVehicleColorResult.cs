namespace GarageFlow.Application.Vehicles.VehicleColors.CreateVehicleColor;

public sealed record CreateVehicleColorResult(
    Guid Id,
    string Name,
    DateTime CreatedAt);
