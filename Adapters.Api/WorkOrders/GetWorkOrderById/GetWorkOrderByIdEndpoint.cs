using GarageFlow.Adapters.Api.WorkOrders.Responses;
using GarageFlow.Application.WorkOrders.GetWorkOrderById;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.GetWorkOrderById;

public static class GetWorkOrderByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetWorkOrderByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/work-orders/{id:guid}", GetWorkOrderById)
            .WithName("GetWorkOrderById")
            .WithTags("Work Orders")
            .WithSummary("Get work order details by identifier")
            .Produces<WorkOrderDetailsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetWorkOrderById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWorkOrderByIdQuery(id), cancellationToken);
        var response = WorkOrderResponseMapper.MapDetails(result);

        return Results.Ok(response);
    }
}
