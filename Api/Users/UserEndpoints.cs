using GarageFlow.Api.Users.ChangeMyPassword;
using GarageFlow.Api.Users.CreateUser;
using GarageFlow.Api.Users.DeleteUser;
using GarageFlow.Api.Users.ListUsers;
using GarageFlow.Api.Users.UpdateMyProfile;

namespace GarageFlow.Api.Users;

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
