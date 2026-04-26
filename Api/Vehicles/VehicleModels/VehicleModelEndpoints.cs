using GarageFlow.Api.Vehicles.VehicleModels.CreateVehicleModel;
using GarageFlow.Api.Vehicles.VehicleModels.DeleteVehicleModel;
using GarageFlow.Api.Vehicles.VehicleModels.GetVehicleModelById;
using GarageFlow.Api.Vehicles.VehicleModels.ListVehicleModels;
using GarageFlow.Api.Vehicles.VehicleModels.UpdateVehicleModel;

namespace GarageFlow.Api.Vehicles.VehicleModels;

public static class VehicleModelEndpoints
{
    public static IEndpointRouteBuilder MapVehicleModelEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateVehicleModelEndpoint();
        app.MapGetVehicleModelByIdEndpoint();
        app.MapListVehicleModelsEndpoint();
        app.MapUpdateVehicleModelEndpoint();
        app.MapDeleteVehicleModelEndpoint();
        return app;
    }
}
