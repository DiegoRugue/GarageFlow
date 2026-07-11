using GarageFlow.Adapters.Api.InventoryItems.GetInventoryItemById;
using GarageFlow.Application.InventoryItems.UseCases.UpdateInventoryItemStock;
using Mediator;

namespace GarageFlow.Adapters.Api.InventoryItems.UpdateInventoryItemStock;

public static class UpdateInventoryItemStockEndpoint
{
    public static IEndpointRouteBuilder MapUpdateInventoryItemStockEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/inventory-items/{id:guid}/stock", UpdateInventoryItemStock)
            .WithName("UpdateInventoryItemStock")
            .WithTags("Inventory Items")
            .WithSummary("Update the stock quantity of an inventory item")
            .Produces<InventoryItemResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> UpdateInventoryItemStock(
        Guid id,
        UpdateInventoryItemStockRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateInventoryItemStockCommand(
            Id: id,
            StockQuantity: request.StockQuantity);

        var result = await mediator.Send(command, cancellationToken);

        var response = new InventoryItemResponse(
            Id: result.Id,
            Name: result.Name,
            Description: result.Description,
            Type: result.Type,
            Cost: result.Cost,
            Price: result.Price,
            StockQuantity: result.StockQuantity,
            CreatedAt: result.CreatedAt);

        return Results.Ok(response);
    }
}
