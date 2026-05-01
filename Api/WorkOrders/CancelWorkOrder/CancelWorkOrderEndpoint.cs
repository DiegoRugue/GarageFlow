using GarageFlow.Application.WorkOrders.CancelWorkOrder;
using Mediator;

namespace GarageFlow.Api.WorkOrders.CancelWorkOrder;

public static class CancelWorkOrderEndpoint
{
    public static IEndpointRouteBuilder MapCancelWorkOrderEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/cancel", CancelWorkOrder)
            .WithName("CancelWorkOrder")
            .WithTags("Work Orders")
            .WithSummary("Cancel a work order")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CancelWorkOrder(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelWorkOrderCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
