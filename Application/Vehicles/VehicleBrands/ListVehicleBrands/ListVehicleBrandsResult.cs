using GarageFlow.Application.Vehicles.VehicleBrands.GetVehicleBrandById;

namespace GarageFlow.Application.Vehicles.VehicleBrands.ListVehicleBrands;

public sealed record ListVehicleBrandsResult(
    IReadOnlyList<VehicleBrandDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
