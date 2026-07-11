using GarageFlow.Adapters.Api.Users.ChangeMyPassword;
using GarageFlow.Adapters.Api.Users.CreateUser;
using GarageFlow.Adapters.Api.Users.DeleteUser;
using GarageFlow.Adapters.Api.Users.ListUsers;
using GarageFlow.Adapters.Api.Users.UpdateMyProfile;

namespace GarageFlow.Adapters.Api.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateUserEndpoint();
        app.MapListUsersEndpoint();
        app.MapDeleteUserEndpoint();
        app.MapUpdateMyProfileEndpoint();
        app.MapChangeMyPasswordEndpoint();
        return app;
    }
}
