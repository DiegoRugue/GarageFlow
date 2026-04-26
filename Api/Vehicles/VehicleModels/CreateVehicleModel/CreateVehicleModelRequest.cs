namespace GarageFlow.Api.Vehicles.VehicleModels.CreateVehicleModel;

public sealed record CreateVehicleModelRequest(
    Guid VehicleBrandId,
    string Name);
