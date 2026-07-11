using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.ListVehicleBrands;

public sealed record ListVehicleBrandsQuery(int Page = 1, int PageSize = 20) : IRequest<ListVehicleBrandsResult>;
