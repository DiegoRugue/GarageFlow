namespace GarageFlow.Adapters.Api.Vehicles.CreateVehicle;

public sealed record CreateVehicleResponse(
    Guid Id,
    Guid CustomerId,
    int Year,
    Guid VehicleBrandId,
    Guid VehicleModelId,
    Guid VehicleColorId,
    string Plate,
    DateTime CreatedAt);
