using GarageFlow.Application.WorkOrders.DeliverWorkOrder;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.DeliverWorkOrder;

public static class DeliverWorkOrderEndpoint
{
    public static IEndpointRouteBuilder MapDeliverWorkOrderEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/deliver", DeliverWorkOrder)
            .WithName("DeliverWorkOrder")
            .WithTags("Work Orders")
            .WithSummary("Deliver a completed work order")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> DeliverWorkOrder(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeliverWorkOrderCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
