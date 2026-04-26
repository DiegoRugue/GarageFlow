namespace GarageFlow.Api.Vehicles.VehicleColors.CreateVehicleColor;

public sealed record CreateVehicleColorResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt);
