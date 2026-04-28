using GarageFlow.Api.WorkOrders.Responses;
using GarageFlow.Application.WorkOrders.GetWorkOrderById;
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
            Items: result.Items.Select(MapDetails).ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }

    private static WorkOrderDetailsResponse MapDetails(WorkOrderDetailsDto details)
    {
        return new WorkOrderDetailsResponse(
            Id: details.Id,
            CustomerId: details.CustomerId,
            VehicleId: details.VehicleId,
            Status: details.Status,
            CreatedAt: details.CreatedAt,
            UpdatedAt: details.UpdatedAt,
            Estimates: details.Estimates.Select(MapEstimate).ToList());
    }

    private static WorkOrderEstimateResponse MapEstimate(WorkOrderEstimateDto estimate)
    {
        return new WorkOrderEstimateResponse(
            Id: estimate.Id,
            WorkOrderId: estimate.WorkOrderId,
            Status: estimate.Status,
            TotalAmount: estimate.TotalAmount,
            CreatedAt: estimate.CreatedAt,
            UpdatedAt: estimate.UpdatedAt,
            InventoryLines: estimate.InventoryLines.Select(MapInventoryLine).ToList(),
            ServiceLines: estimate.ServiceLines.Select(MapServiceLine).ToList());
    }

    private static WorkOrderInventoryLineResponse MapInventoryLine(WorkOrderInventoryLineDto line)
    {
        return new WorkOrderInventoryLineResponse(
            Id: line.Id,
            EstimateId: line.EstimateId,
            InventoryItemId: line.InventoryItemId,
            Description: line.Description,
            Quantity: line.Quantity,
            UnitCost: line.UnitCost,
            UnitPrice: line.UnitPrice,
            TotalPrice: line.TotalPrice);
    }

    private static WorkOrderServiceLineResponse MapServiceLine(WorkOrderServiceLineDto line)
    {
        return new WorkOrderServiceLineResponse(
            Id: line.Id,
            EstimateId: line.EstimateId,
            ServiceId: line.ServiceId,
            Description: line.Description,
            UnitPrice: line.UnitPrice,
            TotalPrice: line.TotalPrice);
    }
}
