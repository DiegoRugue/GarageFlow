using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.GetVehicleModelById;

public sealed record GetVehicleModelByIdQuery(Guid Id) : IRequest<VehicleModelDto>;
