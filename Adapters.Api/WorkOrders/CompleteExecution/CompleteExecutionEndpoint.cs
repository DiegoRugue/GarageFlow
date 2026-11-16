using GarageFlow.Application.WorkOrders.UseCases.CompleteExecution;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.CompleteExecution;

public static class CompleteExecutionEndpoint
{
    public static IEndpointRouteBuilder MapCompleteExecutionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/execution/complete", Handle)
            .WithName("CompleteExecution")
            .WithTags("Work Orders")
            .WithSummary("Request complete of distributed work order execution")
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
        await mediator.Send(new CompleteExecutionCommand(id), cancellationToken);
        return Results.Accepted($"/work-orders/{id:D}/status");
    }
}
