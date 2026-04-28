using GarageFlow.Api.InventoryItems.GetInventoryItemById;
using GarageFlow.Application.InventoryItems.UpdateInventoryItem;
using Mediator;

namespace GarageFlow.Api.InventoryItems.UpdateInventoryItem;

public static class UpdateInventoryItemEndpoint
{
    public static IEndpointRouteBuilder MapUpdateInventoryItemEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/inventory-items/{id:guid}", UpdateInventoryItem)
            .WithName("UpdateInventoryItem")
            .WithTags("Inventory Items")
            .WithSummary("Update an existing inventory item")
            .Produces<InventoryItemResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> UpdateInventoryItem(
        Guid id,
        UpdateInventoryItemRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateInventoryItemCommand(
            Id: id,
            Name: request.Name,
            Description: request.Description,
            Type: request.Type,
            Cost: request.Cost,
            Price: request.Price);

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
