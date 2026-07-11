using GarageFlow.Adapters.Api.Vehicles.GetVehicleById;

namespace GarageFlow.Adapters.Api.Vehicles.ListVehicles;

public sealed record ListVehiclesResponse(
    IReadOnlyList<VehicleResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
