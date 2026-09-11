using GarageFlow.Application.Auth.UseCases.VerifyCustomerCredentials;
using Mediator;

namespace GarageFlow.Adapters.Api.Auth.VerifyCustomerCredentials;

public static class VerifyCustomerCredentialsEndpoint
{
    public static IEndpointRouteBuilder MapVerifyCustomerCredentialsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/verify", Verify)
            .WithName("VerifyCustomerCredentials")
            .WithTags("InternalAuth")
            .WithSummary("Verify customer portal credentials for the authentication function")
            .Produces<VerifyCustomerCredentialsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> Verify(
        VerifyCustomerCredentialsRequest request, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new VerifyCustomerCredentialsQuery(request.Cpf, request.Password), cancellationToken);
        return Results.Ok(new VerifyCustomerCredentialsResponse(
            result.UserId, result.CustomerId, result.Role, result.MustChangePassword));
    }
}
