using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.GetVehicleById;

public sealed record GetVehicleByIdQuery(Guid Id) : IRequest<VehicleDto>;
