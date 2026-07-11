using GarageFlow.Adapters.Api.Vehicles.VehicleBrands.CreateVehicleBrand;
using GarageFlow.Adapters.Api.Vehicles.VehicleBrands.DeleteVehicleBrand;
using GarageFlow.Adapters.Api.Vehicles.VehicleBrands.GetVehicleBrandById;
using GarageFlow.Adapters.Api.Vehicles.VehicleBrands.ListVehicleBrands;
using GarageFlow.Adapters.Api.Vehicles.VehicleBrands.UpdateVehicleBrand;

namespace GarageFlow.Adapters.Api.Vehicles.VehicleBrands;

public static class VehicleBrandEndpoints
{
    public static IEndpointRouteBuilder MapVehicleBrandEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateVehicleBrandEndpoint();
        app.MapGetVehicleBrandByIdEndpoint();
        app.MapListVehicleBrandsEndpoint();
        app.MapUpdateVehicleBrandEndpoint();
        app.MapDeleteVehicleBrandEndpoint();
        return app;
    }
}
