using GarageFlow.Api.Vehicles.VehicleColors.GetVehicleColorById;

namespace GarageFlow.Api.Vehicles.VehicleColors.ListVehicleColors;

public sealed record ListVehicleColorsResponse(
    IReadOnlyList<VehicleColorResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
