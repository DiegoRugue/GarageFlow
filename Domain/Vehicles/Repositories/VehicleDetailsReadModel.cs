namespace GarageFlow.Domain.Vehicles.Repositories;

public sealed record VehicleDetailsReadModel(
    Guid Id,
    Guid CustomerId,
    int Year,
    Guid VehicleBrandId,
    string VehicleBrandName,
    Guid VehicleModelId,
    string VehicleModelName,
    Guid VehicleColorId,
    string VehicleColorName,
    string Plate,
    DateTime CreatedAt);
