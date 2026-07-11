using GarageFlow.Adapters.Api.Security;
using GarageFlow.Application.Users.UseCases.ChangeMyPassword;
using Mediator;

namespace GarageFlow.Adapters.Api.Users.ChangeMyPassword;

public static class ChangeMyPasswordEndpoint
{
    public static IEndpointRouteBuilder MapChangeMyPasswordEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/users/me/password", ChangeMyPassword)
            .RequireAuthorization()
            .WithName("ChangeMyPassword")
            .WithTags("Users")
            .WithSummary("Change authenticated user password")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ChangeMyPassword(
        HttpContext httpContext,
        ChangeMyPasswordRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetRequiredUserId();
        var command = new ChangeMyPasswordCommand(
            UserId: userId,
            CurrentPassword: request.CurrentPassword,
            NewPassword: request.NewPassword);

        await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }
}
