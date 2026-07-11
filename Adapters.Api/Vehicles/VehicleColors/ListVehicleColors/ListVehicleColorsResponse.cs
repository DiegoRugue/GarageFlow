using GarageFlow.Adapters.Api.Vehicles.VehicleColors.GetVehicleColorById;

namespace GarageFlow.Adapters.Api.Vehicles.VehicleColors.ListVehicleColors;

public sealed record ListVehicleColorsResponse(
    IReadOnlyList<VehicleColorResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
