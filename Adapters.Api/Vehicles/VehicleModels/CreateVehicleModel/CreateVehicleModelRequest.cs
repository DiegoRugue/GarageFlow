namespace GarageFlow.Adapters.Api.Vehicles.VehicleModels.CreateVehicleModel;

public sealed record CreateVehicleModelRequest(
    Guid VehicleBrandId,
    string Name);
