namespace GarageFlow.Application.Vehicles.UseCases.UpdateVehicle;

public sealed record UpdateVehicleResult(
    Guid Id,
    Guid CustomerId,
    int Year,
    Guid VehicleBrandId,
    Guid VehicleModelId,
    Guid VehicleColorId,
    string Plate,
    DateTime CreatedAt);
