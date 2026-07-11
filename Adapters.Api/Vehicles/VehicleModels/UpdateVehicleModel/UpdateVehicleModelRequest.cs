namespace GarageFlow.Adapters.Api.Vehicles.VehicleModels.UpdateVehicleModel;

public sealed record UpdateVehicleModelRequest(
    Guid VehicleBrandId,
    string Name);
