using GarageFlow.Adapters.Api.Vehicles.VehicleColors.CreateVehicleColor;
using GarageFlow.Adapters.Api.Vehicles.VehicleColors.DeleteVehicleColor;
using GarageFlow.Adapters.Api.Vehicles.VehicleColors.GetVehicleColorById;
using GarageFlow.Adapters.Api.Vehicles.VehicleColors.ListVehicleColors;
using GarageFlow.Adapters.Api.Vehicles.VehicleColors.UpdateVehicleColor;

namespace GarageFlow.Adapters.Api.Vehicles.VehicleColors;

public static class VehicleColorEndpoints
{
    public static IEndpointRouteBuilder MapVehicleColorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateVehicleColorEndpoint();
        app.MapGetVehicleColorByIdEndpoint();
        app.MapListVehicleColorsEndpoint();
        app.MapUpdateVehicleColorEndpoint();
        app.MapDeleteVehicleColorEndpoint();
        return app;
    }
}
