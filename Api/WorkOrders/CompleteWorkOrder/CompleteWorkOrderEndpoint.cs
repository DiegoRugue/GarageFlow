using GarageFlow.Application.WorkOrders.CompleteWorkOrder;
using Mediator;

namespace GarageFlow.Api.WorkOrders.CompleteWorkOrder;

public static class CompleteWorkOrderEndpoint
{
    public static IEndpointRouteBuilder MapCompleteWorkOrderEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/complete", CompleteWorkOrder)
            .WithName("CompleteWorkOrder")
            .WithTags("Work Orders")
            .WithSummary("Complete a work order")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CompleteWorkOrder(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteWorkOrderCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
