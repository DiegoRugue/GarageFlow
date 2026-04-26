using GarageFlow.Application.Vehicles.VehicleColors.GetVehicleColorById;

namespace GarageFlow.Application.Vehicles.VehicleColors.ListVehicleColors;

public sealed record ListVehicleColorsResult(
    IReadOnlyList<VehicleColorDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
