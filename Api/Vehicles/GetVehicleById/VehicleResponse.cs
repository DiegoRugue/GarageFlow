namespace GarageFlow.Api.Vehicles.GetVehicleById;

public sealed record VehicleResponse(
    Guid Id,
    Guid CustomerId,
    int Year,
    Guid VehicleBrandId,
    Guid VehicleModelId,
    Guid VehicleColorId,
    string Plate,
    DateTime CreatedAt,
    string? VehicleBrandName = null,
    string? VehicleModelName = null,
    string? VehicleColorName = null);
