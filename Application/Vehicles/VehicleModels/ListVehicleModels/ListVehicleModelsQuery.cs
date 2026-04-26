using Mediator;

namespace GarageFlow.Application.Vehicles.VehicleModels.ListVehicleModels;

public sealed record ListVehicleModelsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? VehicleBrandId = null) : IRequest<ListVehicleModelsResult>;
