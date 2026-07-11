using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.UpdateVehicleColor;

public sealed record UpdateVehicleColorCommand(
    Guid Id,
    string Name) : ICommand<UpdateVehicleColorResult>;
