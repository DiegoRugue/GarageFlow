using Mediator;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.ListVehicleColors;

public sealed record ListVehicleColorsQuery(int Page = 1, int PageSize = 20) : IRequest<ListVehicleColorsResult>;
