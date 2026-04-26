namespace GarageFlow.Api.Customers.GetCustomerById;

public sealed record CustomerVehicleResponse(
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
