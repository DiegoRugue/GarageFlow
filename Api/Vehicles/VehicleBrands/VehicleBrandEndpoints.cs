using GarageFlow.Api.Vehicles.VehicleBrands.CreateVehicleBrand;
using GarageFlow.Api.Vehicles.VehicleBrands.DeleteVehicleBrand;
using GarageFlow.Api.Vehicles.VehicleBrands.GetVehicleBrandById;
using GarageFlow.Api.Vehicles.VehicleBrands.ListVehicleBrands;
using GarageFlow.Api.Vehicles.VehicleBrands.UpdateVehicleBrand;

namespace GarageFlow.Api.Vehicles.VehicleBrands;

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
