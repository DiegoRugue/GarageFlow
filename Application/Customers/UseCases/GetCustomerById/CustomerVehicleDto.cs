namespace GarageFlow.Application.Customers.UseCases.GetCustomerById;

public sealed record CustomerVehicleDto(
    Guid Id,
    int Year,
    string Plate,
    Guid VehicleBrandId,
    string VehicleBrandName,
    Guid VehicleModelId,
    string VehicleModelName,
    Guid VehicleColorId,
    string VehicleColorName,
    DateTime CreatedAt);
