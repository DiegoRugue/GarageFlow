using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.DeleteVehicleColor;

public sealed record DeleteVehicleColorCommand(Guid Id) : IRequest<Unit>;
