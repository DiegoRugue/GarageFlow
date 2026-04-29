using GarageFlow.Api.Security;
using GarageFlow.Api.WorkOrders.Responses;
using GarageFlow.Application.WorkOrders;
using GarageFlow.Application.WorkOrders.ListMyWorkOrders;
using Mediator;

namespace GarageFlow.Api.WorkOrders.ListMyWorkOrders;

public static class ListMyWorkOrdersEndpoint
{
    public static IEndpointRouteBuilder MapListMyWorkOrdersEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me/work-orders", ListMyWorkOrders)
            .WithName("ListMyWorkOrders")
            .WithTags("Work Orders")
            .WithSummary("List authenticated customer work orders")
            .Produces<ListMyWorkOrdersResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListMyWorkOrders(
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var userId = httpContext.User.GetRequiredUserId();
        var result = await mediator.Send(new ListMyWorkOrdersQuery(userId, page, pageSize), cancellationToken);

        var response = new ListMyWorkOrdersResponse(
            Items: result.Items.Select(WorkOrderResponseMapper.MapDetails).ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }
}
