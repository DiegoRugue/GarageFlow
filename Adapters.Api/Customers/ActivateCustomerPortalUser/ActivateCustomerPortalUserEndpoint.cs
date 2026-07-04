using GarageFlow.Adapters.Api.Security;
using GarageFlow.Application.Customers.ActivateCustomerPortalUser;
using Mediator;

namespace GarageFlow.Adapters.Api.Customers.ActivateCustomerPortalUser;

public static class ActivateCustomerPortalUserEndpoint
{
    public static IEndpointRouteBuilder MapActivateCustomerPortalUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/customers/{customerId:guid}/portal-user", ActivateCustomerPortalUser)
            .RequireAuthorization(SecurityPolicies.ActiveStaff)
            .WithName("ActivateCustomerPortalUser")
            .WithTags("Customers")
            .WithSummary("Activate customer portal access")
            .Produces<ActivateCustomerPortalUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ActivateCustomerPortalUser(
        Guid customerId,
        ActivateCustomerPortalUserRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new ActivateCustomerPortalUserCommand(customerId, request.BirthDate),
            cancellationToken);

        var response = new ActivateCustomerPortalUserResponse(
            result.Id,
            result.CustomerId,
            result.FullName,
            result.Email,
            result.BirthDate,
            result.Role,
            result.MustChangePassword,
            result.CreatedAt);

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }
}
