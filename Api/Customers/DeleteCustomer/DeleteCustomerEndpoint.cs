using GarageFlow.Application.Customers.DeleteCustomer;
using Mediator;

namespace GarageFlow.Api.Customers.DeleteCustomer;

public static class DeleteCustomerEndpoint
{
    public static IEndpointRouteBuilder MapDeleteCustomerEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/customers/{id:guid}", DeleteCustomer)
            .WithName("DeleteCustomer")
            .WithTags("Customers")
            .WithSummary("Delete an existing customer by ID")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> DeleteCustomer(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteCustomerCommand(Id: id);
        await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }
}
