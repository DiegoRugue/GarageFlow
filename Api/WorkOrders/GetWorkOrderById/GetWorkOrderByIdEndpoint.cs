using GarageFlow.Api.WorkOrders.Responses;
using GarageFlow.Application.WorkOrders.GetWorkOrderById;
using Mediator;

namespace GarageFlow.Api.WorkOrders.GetWorkOrderById;

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
        var response = MapDetails(result);

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
