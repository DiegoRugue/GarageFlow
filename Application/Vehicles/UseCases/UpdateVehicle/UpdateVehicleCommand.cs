using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.UpdateVehicle;

public sealed record UpdateVehicleCommand(
    Guid Id,
    string Plate,
    int Year,
    Guid CustomerId,
    Guid VehicleModelId,
    Guid VehicleColorId) : ICommand<UpdateVehicleResult>;
