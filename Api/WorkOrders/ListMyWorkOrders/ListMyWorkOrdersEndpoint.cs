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
            Items: result.Items.Select(MapDetails).ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }

    private static CustomerWorkOrderDetailsResponse MapDetails(CustomerWorkOrderDetailsDto details)
    {
        return new CustomerWorkOrderDetailsResponse(
            Id: details.Id,
            CustomerId: details.CustomerId,
            VehicleId: details.VehicleId,
            Status: details.Status,
            CreatedAt: details.CreatedAt,
            UpdatedAt: details.UpdatedAt,
            Estimates: details.Estimates.Select(MapEstimate).ToList());
    }

    private static CustomerWorkOrderEstimateResponse MapEstimate(CustomerWorkOrderEstimateDto estimate)
    {
        return new CustomerWorkOrderEstimateResponse(
            Id: estimate.Id,
            WorkOrderId: estimate.WorkOrderId,
            Status: estimate.Status,
            TotalAmount: estimate.TotalAmount,
            CreatedAt: estimate.CreatedAt,
            UpdatedAt: estimate.UpdatedAt,
            InventoryLines: estimate.InventoryLines.Select(MapInventoryLine).ToList(),
            ServiceLines: estimate.ServiceLines.Select(MapServiceLine).ToList());
    }

    private static CustomerWorkOrderInventoryLineResponse MapInventoryLine(CustomerWorkOrderInventoryLineDto line)
    {
        return new CustomerWorkOrderInventoryLineResponse(
            Id: line.Id,
            EstimateId: line.EstimateId,
            InventoryItemId: line.InventoryItemId,
            Description: line.Description,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            TotalPrice: line.TotalPrice);
    }

    private static CustomerWorkOrderServiceLineResponse MapServiceLine(CustomerWorkOrderServiceLineDto line)
    {
        return new CustomerWorkOrderServiceLineResponse(
            Id: line.Id,
            EstimateId: line.EstimateId,
            ServiceId: line.ServiceId,
            Description: line.Description,
            UnitPrice: line.UnitPrice,
            TotalPrice: line.TotalPrice);
    }
}
