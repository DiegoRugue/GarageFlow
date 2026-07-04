namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.CreateVehicleColor;

public sealed record CreateVehicleColorResult(
    Guid Id,
    string Name,
    DateTime CreatedAt);
