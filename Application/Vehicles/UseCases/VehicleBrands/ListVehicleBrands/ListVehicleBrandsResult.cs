using GarageFlow.Application.Vehicles.UseCases.VehicleBrands.GetVehicleBrandById;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleBrands.ListVehicleBrands;

public sealed record ListVehicleBrandsResult(
    IReadOnlyList<VehicleBrandDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
