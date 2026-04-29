using GarageFlow.Api.WorkOrders.Responses;
using GarageFlow.Application.WorkOrders.ListWorkOrders;
using Mediator;

namespace GarageFlow.Api.WorkOrders.ListWorkOrders;

public static class ListWorkOrdersEndpoint
{
    public static IEndpointRouteBuilder MapListWorkOrdersEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/work-orders", ListWorkOrders)
            .WithName("ListWorkOrders")
            .WithTags("Work Orders")
            .WithSummary("List work orders with pagination")
            .Produces<ListWorkOrdersResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListWorkOrders(
        IMediator mediator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        Guid? customerId = null)
    {
        var result = await mediator.Send(new ListWorkOrdersQuery(page, pageSize, customerId), cancellationToken);

        var response = new ListWorkOrdersResponse(
            Items: result.Items.Select(WorkOrderResponseMapper.MapDetails).ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }
}
