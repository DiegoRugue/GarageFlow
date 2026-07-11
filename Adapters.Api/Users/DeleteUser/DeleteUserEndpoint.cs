using GarageFlow.Adapters.Api.Security;
using GarageFlow.Application.Users.UseCases.DeleteUser;
using Mediator;

namespace GarageFlow.Adapters.Api.Users.DeleteUser;

public static class DeleteUserEndpoint
{
    public static IEndpointRouteBuilder MapDeleteUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/users/{id:guid}", DeleteUser)
            .RequireAuthorization(SecurityPolicies.ActiveAdmin)
            .WithName("DeleteUser")
            .WithTags("Users")
            .WithSummary("Delete an existing user by ID")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> DeleteUser(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteUserCommand(Id: id);
        await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }
}
