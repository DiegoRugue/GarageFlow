using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Events;

public sealed record VehicleModelCreated(
    VehicleModelId VehicleModelId,
    VehicleBrandId VehicleBrandId,
    string Name,
    DateTime CreatedAt) : DomainEvent;
