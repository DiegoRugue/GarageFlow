using GarageFlow.Application.WorkOrders.UseCases.AddEstimateInventoryItem;
using Mediator;

namespace GarageFlow.Adapters.Api.WorkOrders.AddEstimateInventoryItem;

public static class AddEstimateInventoryItemEndpoint
{
    public static IEndpointRouteBuilder MapAddEstimateInventoryItemEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/work-orders/{id:guid}/estimates/{estimateId:guid}/inventory-items", AddEstimateInventoryItem)
            .WithName("AddEstimateInventoryItem")
            .WithTags("Work Orders")
            .WithSummary("Add an inventory line to a work order estimate")
            .Produces<AddEstimateInventoryItemResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> AddEstimateInventoryItem(
        Guid id,
        Guid estimateId,
        AddEstimateInventoryItemRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AddEstimateInventoryItemCommand(
            WorkOrderId: id,
            EstimateId: estimateId,
            InventoryItemId: request.InventoryItemId,
            Quantity: request.Quantity);

        var result = await mediator.Send(command, cancellationToken);
        var response = new AddEstimateInventoryItemResponse(
            EstimateId: result.EstimateId,
            InventoryItemId: result.InventoryItemId,
            Description: result.Description,
            Quantity: result.Quantity,
            UnitCost: result.UnitCost,
            UnitPrice: result.UnitPrice,
            TotalPrice: result.TotalPrice);

        return Results.Ok(response);
    }
}
