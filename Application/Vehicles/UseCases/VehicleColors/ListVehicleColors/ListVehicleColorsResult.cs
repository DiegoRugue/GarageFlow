using GarageFlow.Application.Vehicles.UseCases.VehicleColors.GetVehicleColorById;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleColors.ListVehicleColors;

public sealed record ListVehicleColorsResult(
    IReadOnlyList<VehicleColorDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
