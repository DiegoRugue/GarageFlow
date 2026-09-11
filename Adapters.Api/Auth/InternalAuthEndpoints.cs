using GarageFlow.Adapters.Api.Auth.VerifyCustomerCredentials;
using GarageFlow.Adapters.Api.Security;

namespace GarageFlow.Adapters.Api.Auth;

public static class InternalAuthEndpoints
{
    public static IEndpointRouteBuilder MapInternalAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/auth/customer-credentials")
            .RequireAuthorization(SecurityPolicies.VerifyCustomerCredentials);
        group.MapVerifyCustomerCredentialsEndpoint();
        return app;
    }
}
