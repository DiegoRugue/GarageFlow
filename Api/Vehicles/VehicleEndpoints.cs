using GarageFlow.Api.Security;
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
        var protectedVehicleRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveUser);

        protectedVehicleRoutes.MapCreateVehicleEndpoint();
        protectedVehicleRoutes.MapGetVehicleByIdEndpoint();
        protectedVehicleRoutes.MapListVehiclesEndpoint();
        protectedVehicleRoutes.MapUpdateVehicleEndpoint();
        protectedVehicleRoutes.MapDeleteVehicleEndpoint();

        protectedVehicleRoutes.MapVehicleBrandEndpoints();
        protectedVehicleRoutes.MapVehicleModelEndpoints();
        protectedVehicleRoutes.MapVehicleColorEndpoints();

        return app;
    }
}
