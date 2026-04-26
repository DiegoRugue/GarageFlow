using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleColors.UpdateVehicleColor;

public sealed record UpdateVehicleColorCommand(
    Guid Id,
    string Name) : IRequest<UpdateVehicleColorResult>;
