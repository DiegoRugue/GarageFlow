using GarageFlow.Api.Security;
using GarageFlow.Api.WorkOrders.Responses;
using GarageFlow.Application.WorkOrders;
using GarageFlow.Application.WorkOrders.GetMyWorkOrderById;
using Mediator;

namespace GarageFlow.Api.WorkOrders.GetMyWorkOrderById;

public static class GetMyWorkOrderByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetMyWorkOrderByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me/work-orders/{id:guid}", GetMyWorkOrderById)
            .WithName("GetMyWorkOrderById")
            .WithTags("Work Orders")
            .WithSummary("Get authenticated customer work order details by identifier")
            .Produces<CustomerWorkOrderDetailsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetMyWorkOrderById(
        Guid id,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetRequiredUserId();
        var result = await mediator.Send(new GetMyWorkOrderByIdQuery(userId, id), cancellationToken);
        var response = WorkOrderResponseMapper.MapDetails(result);

        return Results.Ok(response);
    }
}
