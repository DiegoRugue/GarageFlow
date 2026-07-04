using GarageFlow.Application.Vehicles.UseCases.GetVehicleById;

namespace GarageFlow.Application.Vehicles.UseCases.ListVehicles;

public sealed record ListVehiclesResult(
    IReadOnlyList<VehicleDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
