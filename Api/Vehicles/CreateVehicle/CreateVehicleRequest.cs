namespace GarageFlow.Api.Vehicles.CreateVehicle;

public sealed record CreateVehicleRequest(
    string Plate,
    int Year,
    Guid CustomerId,
    Guid VehicleModelId,
    Guid VehicleColorId);
