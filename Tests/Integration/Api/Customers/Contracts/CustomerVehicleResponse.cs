namespace GarageFlow.Tests.Integration.Api.Customers.Contracts;

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
