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
        var response = MapDetails(result);

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
