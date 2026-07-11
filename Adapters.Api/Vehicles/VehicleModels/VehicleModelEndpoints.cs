using GarageFlow.Adapters.Api.Vehicles.VehicleModels.CreateVehicleModel;
using GarageFlow.Adapters.Api.Vehicles.VehicleModels.DeleteVehicleModel;
using GarageFlow.Adapters.Api.Vehicles.VehicleModels.GetVehicleModelById;
using GarageFlow.Adapters.Api.Vehicles.VehicleModels.ListVehicleModels;
using GarageFlow.Adapters.Api.Vehicles.VehicleModels.UpdateVehicleModel;

namespace GarageFlow.Adapters.Api.Vehicles.VehicleModels;

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
