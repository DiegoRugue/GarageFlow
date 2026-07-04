using GarageFlow.Adapters.Api.Security;
using GarageFlow.Adapters.Api.Services.CreateService;
using GarageFlow.Adapters.Api.Services.DeleteService;
using GarageFlow.Adapters.Api.Services.GetServiceById;
using GarageFlow.Adapters.Api.Services.ListServices;
using GarageFlow.Adapters.Api.Services.UpdateService;

namespace GarageFlow.Adapters.Api.Services;

public static class ServiceEndpoints
{
    public static IEndpointRouteBuilder MapServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var protectedServiceRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveStaff);

        protectedServiceRoutes.MapCreateServiceEndpoint();
        protectedServiceRoutes.MapGetServiceByIdEndpoint();
        protectedServiceRoutes.MapListServicesEndpoint();
        protectedServiceRoutes.MapUpdateServiceEndpoint();
        protectedServiceRoutes.MapDeleteServiceEndpoint();
        return app;
    }
}
