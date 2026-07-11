using GarageFlow.Adapters.Api.Vehicles.VehicleModels.GetVehicleModelById;

namespace GarageFlow.Adapters.Api.Vehicles.VehicleModels.ListVehicleModels;

public sealed record ListVehicleModelsResponse(
    IReadOnlyList<VehicleModelResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
