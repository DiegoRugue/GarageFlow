using GarageFlow.Api.Security;
using GarageFlow.Application.Users.CreateUser;
using Mediator;

namespace GarageFlow.Api.Users.CreateUser;

public static class CreateUserEndpoint
{
    public static IEndpointRouteBuilder MapCreateUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/users", CreateUser)
            .RequireAuthorization(SecurityPolicies.ActiveAdmin)
            .WithName("CreateUser")
            .WithTags("Users")
            .WithSummary("Create a new user")
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateUser(
        CreateUserRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(
            FullName: request.FullName,
            Email: request.Email,
            BirthDate: request.BirthDate,
            Role: request.Role);

        var result = await mediator.Send(command, cancellationToken);
        var response = new CreateUserResponse(
            Id: result.Id,
            FullName: result.FullName,
            Email: result.Email,
            BirthDate: result.BirthDate,
            Role: result.Role,
            MustChangePassword: result.MustChangePassword,
            CreatedAt: result.CreatedAt);

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }
}
