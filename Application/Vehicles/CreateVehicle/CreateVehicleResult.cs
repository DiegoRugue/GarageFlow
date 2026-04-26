namespace GarageFlow.Application.Vehicles.CreateVehicle;

public sealed record CreateVehicleResult(
    Guid Id,
    Guid CustomerId,
    int Year,
    Guid VehicleBrandId,
    Guid VehicleModelId,
    Guid VehicleColorId,
    string Plate,
    DateTime CreatedAt);
