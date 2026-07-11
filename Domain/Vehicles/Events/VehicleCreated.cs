using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Events;

public sealed record VehicleCreated(
    VehicleId VehicleId,
    CustomerId CustomerId,
    int Year,
    VehicleBrandId VehicleBrandId,
    VehicleModelId VehicleModelId,
    VehicleColorId VehicleColorId,
    string LicensePlate,
    DateTime CreatedAt) : DomainEvent;
