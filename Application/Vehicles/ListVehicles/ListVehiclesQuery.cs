using Mediator;

namespace GarageFlow.Application.Vehicles.ListVehicles;

public sealed record ListVehiclesQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? CustomerId = null) : IRequest<ListVehiclesResult>;
