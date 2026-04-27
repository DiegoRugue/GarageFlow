using GarageFlow.Api.Security;
using GarageFlow.Api.Services.CreateService;
using GarageFlow.Api.Services.DeleteService;
using GarageFlow.Api.Services.GetServiceById;
using GarageFlow.Api.Services.ListServices;
using GarageFlow.Api.Services.UpdateService;

namespace GarageFlow.Api.Services;

public static class ServiceEndpoints
{
    public static IEndpointRouteBuilder MapServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var protectedServiceRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveUser);

        protectedServiceRoutes.MapCreateServiceEndpoint();
        protectedServiceRoutes.MapGetServiceByIdEndpoint();
        protectedServiceRoutes.MapListServicesEndpoint();
        protectedServiceRoutes.MapUpdateServiceEndpoint();
        protectedServiceRoutes.MapDeleteServiceEndpoint();
        return app;
    }
}

