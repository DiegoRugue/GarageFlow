using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleModels.GetVehicleModelById;

public sealed record GetVehicleModelByIdQuery(Guid Id) : IRequest<VehicleModelDto>;
