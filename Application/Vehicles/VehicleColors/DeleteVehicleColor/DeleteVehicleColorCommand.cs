using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleColors.DeleteVehicleColor;

public sealed record DeleteVehicleColorCommand(Guid Id) : IRequest<Unit>;
