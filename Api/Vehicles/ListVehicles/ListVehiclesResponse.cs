using GarageFlow.Api.Vehicles.GetVehicleById;

namespace GarageFlow.Api.Vehicles.ListVehicles;

public sealed record ListVehiclesResponse(
    IReadOnlyList<VehicleResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
