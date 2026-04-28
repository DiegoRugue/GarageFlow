using Mediator;

namespace GarageFlow.Application.Vehicles.GetVehicleById;

public sealed record GetVehicleByIdQuery(Guid Id) : IRequest<VehicleDto>;
