using GarageFlow.Api.Vehicles.CreateVehicle;
using GarageFlow.Api.Vehicles.DeleteVehicle;
using GarageFlow.Api.Vehicles.GetVehicleById;
using GarageFlow.Api.Vehicles.ListVehicles;
using GarageFlow.Api.Vehicles.UpdateVehicle;
using GarageFlow.Api.Vehicles.VehicleBrands;
using GarageFlow.Api.Vehicles.VehicleColors;
using GarageFlow.Api.Vehicles.VehicleModels;

namespace GarageFlow.Api.Vehicles;

public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateVehicleEndpoint();
        app.MapGetVehicleByIdEndpoint();
        app.MapListVehiclesEndpoint();
        app.MapUpdateVehicleEndpoint();
        app.MapDeleteVehicleEndpoint();

        app.MapVehicleBrandEndpoints();
        app.MapVehicleModelEndpoints();
        app.MapVehicleColorEndpoints();

        return app;
    }
}
