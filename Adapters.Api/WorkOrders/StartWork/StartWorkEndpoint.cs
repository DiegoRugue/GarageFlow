using GarageFlow.Application.WorkOrders.StartWork;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.StartWork;

public static class StartWorkEndpoint
{
    public static IEndpointRouteBuilder MapStartWorkEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/start-work", StartWork)
            .WithName("StartWork")
            .WithTags("Work Orders")
            .WithSummary("Start work on a work order")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> StartWork(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new StartWorkCommand(id), cancellationToken);
        return Results.NoContent();
    }
}
