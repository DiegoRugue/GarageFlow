using GarageFlow.Adapters.Api.Security;
using GarageFlow.Application.WorkOrders.UseCases.ApproveMyEstimate;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.ApproveMyEstimate;

public static class ApproveMyEstimateEndpoint
{
    public static IEndpointRouteBuilder MapApproveMyEstimateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/me/work-orders/{workOrderId:guid}/estimates/{estimateId:guid}/approve", ApproveMyEstimate)
            .WithName("ApproveMyEstimate")
            .WithTags("Work Orders")
            .WithSummary("Approve an estimate for an authenticated customer work order")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ApproveMyEstimate(
        Guid workOrderId,
        Guid estimateId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetRequiredUserId();
        await mediator.Send(new ApproveMyEstimateCommand(userId, workOrderId, estimateId), cancellationToken);
        return Results.NoContent();
    }
}
