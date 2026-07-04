using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.UpdateVehicleColor;

public sealed record UpdateVehicleColorCommand(
    Guid Id,
    string Name) : IRequest<UpdateVehicleColorResult>;
