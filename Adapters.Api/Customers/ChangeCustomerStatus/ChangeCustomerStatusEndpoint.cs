using GarageFlow.Adapters.Api.Security;
using GarageFlow.Application.Customers.UseCases.ChangeCustomerStatus;
using Mediator;

namespace GarageFlow.Adapters.Api.Customers.ChangeCustomerStatus;

public static class ChangeCustomerStatusEndpoint
{
    public static IEndpointRouteBuilder MapChangeCustomerStatusEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/customers/{id:guid}/status", ChangeCustomerStatus)
            .RequireAuthorization(SecurityPolicies.ActiveAdmin)
            .WithName("ChangeCustomerStatus")
            .WithTags("Customers")
            .WithSummary("Change a customer's access status")
            .Produces<ChangeCustomerStatusResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ChangeCustomerStatus(
        Guid id,
        ChangeCustomerStatusRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new ChangeCustomerStatusCommand(
            CustomerId: id,
            Status: request.Status);
        var result = await mediator.Send(command, cancellationToken);

        return Results.Ok(new ChangeCustomerStatusResponse(
            Id: result.Id,
            Status: result.Status));
    }
}
