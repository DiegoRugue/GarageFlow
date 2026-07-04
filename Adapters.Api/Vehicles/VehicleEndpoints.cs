using GarageFlow.Adapters.Api.Security;
using GarageFlow.Adapters.Api.Vehicles.CreateVehicle;
using GarageFlow.Adapters.Api.Vehicles.DeleteVehicle;
using GarageFlow.Adapters.Api.Vehicles.GetVehicleById;
using GarageFlow.Adapters.Api.Vehicles.ListVehicles;
using GarageFlow.Adapters.Api.Vehicles.UpdateVehicle;
using GarageFlow.Adapters.Api.Vehicles.VehicleBrands;
using GarageFlow.Adapters.Api.Vehicles.VehicleColors;
using GarageFlow.Adapters.Api.Vehicles.VehicleModels;

namespace GarageFlow.Adapters.Api.Vehicles;

public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder app)
    {
        var protectedVehicleRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveStaff);

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
