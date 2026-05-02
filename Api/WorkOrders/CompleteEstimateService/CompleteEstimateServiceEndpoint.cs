using GarageFlow.Application.WorkOrders.CompleteEstimateService;
using Mediator;

namespace GarageFlow.Api.WorkOrders.CompleteEstimateService;

public static class CompleteEstimateServiceEndpoint
{
    public static IEndpointRouteBuilder MapCompleteEstimateServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/estimates/{estimateId:guid}/services/{lineId:guid}/complete", CompleteEstimateService)
            .WithName("CompleteEstimateService")
            .WithTags("Work Orders")
            .WithSummary("Complete an in-progress estimate service line")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CompleteEstimateService(
        Guid id,
        Guid estimateId,
        Guid lineId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteEstimateServiceCommand(id, estimateId, lineId), cancellationToken);
        return Results.NoContent();
    }
}
