using GarageFlow.Application.Auth.Login;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Api.Auth.Login;

public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapLoginEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", Login)
            .AllowAnonymous()
            .WithName("Login")
            .WithTags("Auth")
            .WithSummary("Authenticate user and issue JWT token")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(
            Email: request.Email,
            Password: request.Password);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            var response = new LoginResponse(
                Token: result.Token,
                MustChangePassword: result.MustChangePassword);

            return Results.Ok(response);
        }
        catch (BusinessRuleViolationException exception)
        {
            return Results.Problem(
                title: "Unauthorized",
                detail: exception.Message,
                statusCode: StatusCodes.Status401Unauthorized);
        }
    }
}
