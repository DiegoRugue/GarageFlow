using GarageFlow.Adapters.Api.Security;
using GarageFlow.Application.WorkOrders.UseCases.RejectMyEstimate;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.RejectMyEstimate;

public static class RejectMyEstimateEndpoint
{
    public static IEndpointRouteBuilder MapRejectMyEstimateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/me/work-orders/{workOrderId:guid}/estimates/{estimateId:guid}/reject", RejectMyEstimate)
            .WithName("RejectMyEstimate")
            .WithTags("Work Orders")
            .WithSummary("Reject an estimate for an authenticated customer work order")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> RejectMyEstimate(
        Guid workOrderId,
        Guid estimateId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetRequiredUserId();
        await mediator.Send(new RejectMyEstimateCommand(userId, workOrderId, estimateId), cancellationToken);
        return Results.NoContent();
    }
}
