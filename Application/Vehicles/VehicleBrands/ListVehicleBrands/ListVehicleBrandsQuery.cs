using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleBrands.ListVehicleBrands;

public sealed record ListVehicleBrandsQuery(int Page = 1, int PageSize = 20) : IRequest<ListVehicleBrandsResult>;
