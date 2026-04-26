using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleBrands.DeleteVehicleBrand;

public sealed record DeleteVehicleBrandCommand(Guid Id) : IRequest<Unit>;
