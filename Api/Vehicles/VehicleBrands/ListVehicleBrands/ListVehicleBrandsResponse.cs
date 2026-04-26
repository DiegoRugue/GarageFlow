using GarageFlow.Api.Vehicles.VehicleBrands.GetVehicleBrandById;

namespace GarageFlow.Api.Vehicles.VehicleBrands.ListVehicleBrands;

public sealed record ListVehicleBrandsResponse(
    IReadOnlyList<VehicleBrandResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
