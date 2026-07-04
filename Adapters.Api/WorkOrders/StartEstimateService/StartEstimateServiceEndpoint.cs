using GarageFlow.Application.WorkOrders.StartEstimateService;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.StartEstimateService;

public static class StartEstimateServiceEndpoint
{
    public static IEndpointRouteBuilder MapStartEstimateServiceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/estimates/{estimateId:guid}/services/{lineId:guid}/start", StartEstimateService)
            .WithName("StartEstimateService")
            .WithTags("Work Orders")
            .WithSummary("Start an approved estimate service line")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> StartEstimateService(
        Guid id,
        Guid estimateId,
        Guid lineId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new StartEstimateServiceCommand(id, estimateId, lineId), cancellationToken);
        return Results.NoContent();
    }
}
