using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.DeleteVehicle;

public sealed record DeleteVehicleCommand(Guid Id) : IRequest<Unit>;
