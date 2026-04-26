using GarageFlow.Api.Vehicles.VehicleColors.CreateVehicleColor;
using GarageFlow.Api.Vehicles.VehicleColors.DeleteVehicleColor;
using GarageFlow.Api.Vehicles.VehicleColors.GetVehicleColorById;
using GarageFlow.Api.Vehicles.VehicleColors.ListVehicleColors;
using GarageFlow.Api.Vehicles.VehicleColors.UpdateVehicleColor;

namespace GarageFlow.Api.Vehicles.VehicleColors;

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
