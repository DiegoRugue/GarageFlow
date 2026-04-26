using GarageFlow.Application.Vehicles.GetVehicleById;

namespace GarageFlow.Application.Vehicles.ListVehicles;

public sealed record ListVehiclesResult(
    IReadOnlyList<VehicleDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
