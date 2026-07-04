using GarageFlow.Application.Vehicles.UseCases.VehicleModels.GetVehicleModelById;

namespace GarageFlow.Application.Vehicles.UseCases.VehicleModels.ListVehicleModels;

public sealed record ListVehicleModelsResult(
    IReadOnlyList<VehicleModelDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
