using Mediator;

namespace GarageFlow.Application.Vehicles.DeleteVehicle;

public sealed record DeleteVehicleCommand(Guid Id) : IRequest<Unit>;
