using GarageFlow.Application.WorkOrders.UseCases.SubmitEstimate;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.SubmitEstimate;

public static class SubmitEstimateEndpoint
{
    public static IEndpointRouteBuilder MapSubmitEstimateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/estimates/{estimateId:guid}/submit", SubmitEstimate)
            .WithName("SubmitEstimate")
            .WithTags("Work Orders")
            .WithSummary("Submit a work order estimate for customer approval")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> SubmitEstimate(
        Guid id,
        Guid estimateId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new SubmitEstimateCommand(id, estimateId), cancellationToken);
        return Results.NoContent();
    }
}
