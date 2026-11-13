using GarageFlow.Application.WorkOrders.UseCases.StartExecution;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.StartExecution;

public static class StartExecutionEndpoint
{
    public static IEndpointRouteBuilder MapStartExecutionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/execution/start", Handle)
            .WithName("StartExecution")
            .WithTags("Work Orders")
            .WithSummary("Request start of distributed work order execution")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> Handle(Guid id, IMediator mediator, CancellationToken cancellationToken)
    {
        await mediator.Send(new StartExecutionCommand(id), cancellationToken);
        return Results.Accepted($"/work-orders/{id:D}/status");
    }
}
