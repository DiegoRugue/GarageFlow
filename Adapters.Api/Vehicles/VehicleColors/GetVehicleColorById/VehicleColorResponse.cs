namespace GarageFlow.Adapters.Api.Vehicles.VehicleColors.GetVehicleColorById;

public sealed record VehicleColorResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt);
