using GarageFlow.Application.Vehicles.VehicleModels.GetVehicleModelById;

namespace GarageFlow.Application.Vehicles.VehicleModels.ListVehicleModels;

public sealed record ListVehicleModelsResult(
    IReadOnlyList<VehicleModelDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
