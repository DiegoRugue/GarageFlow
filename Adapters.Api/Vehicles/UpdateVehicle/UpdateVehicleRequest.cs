namespace GarageFlow.Adapters.Api.Vehicles.UpdateVehicle;

public sealed record UpdateVehicleRequest(
    string Plate,
    int Year,
    Guid CustomerId,
    Guid VehicleModelId,
    Guid VehicleColorId);
