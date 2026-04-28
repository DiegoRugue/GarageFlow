using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleBrands.GetVehicleBrandById;

public sealed record GetVehicleBrandByIdQuery(Guid Id) : IRequest<VehicleBrandDto>;
