using GarageFlow.Api.Auth.Login;

namespace GarageFlow.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapLoginEndpoint();
        return app;
    }
}
