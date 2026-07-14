using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderStatus;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.GetWorkOrderStatus;

public static class GetWorkOrderStatusEndpoint
{
    public static IEndpointRouteBuilder MapGetWorkOrderStatusEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/work-orders/{id:guid}/status", GetWorkOrderStatus)
            .WithName("GetWorkOrderStatus")
            .WithTags("Work Orders")
            .WithSummary("Get the current status of a work order")
            .Produces<GetWorkOrderStatusResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetWorkOrderStatus(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWorkOrderStatusQuery(id), cancellationToken);
        return Results.Ok(new GetWorkOrderStatusResponse(result.Id, result.Status, result.UpdatedAt));
    }
}
