using GarageFlow.Adapters.Api.Auth.Login;

namespace GarageFlow.Adapters.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapLoginEndpoint();
        return app;
    }
}
