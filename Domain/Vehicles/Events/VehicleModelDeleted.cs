using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.Domain.Vehicles.ValueObjects;

namespace GarageFlow.Domain.Vehicles.Events;

public sealed record VehicleModelDeleted(
    VehicleModelId VehicleModelId,
    VehicleBrandId VehicleBrandId,
    string Name) : DomainEvent;
