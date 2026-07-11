using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.CreateVehicle;

public sealed record CreateVehicleCommand(
    string Plate,
    int Year,
    Guid CustomerId,
    Guid VehicleModelId,
    Guid VehicleColorId) : ICommand<CreateVehicleResult>;
