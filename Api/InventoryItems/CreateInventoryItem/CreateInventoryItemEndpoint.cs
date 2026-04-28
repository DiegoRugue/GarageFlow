using GarageFlow.Application.InventoryItems.CreateInventoryItem;
using Mediator;

namespace GarageFlow.Api.InventoryItems.CreateInventoryItem;

public static class CreateInventoryItemEndpoint
{
    public static IEndpointRouteBuilder MapCreateInventoryItemEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/inventory-items", CreateInventoryItem)
            .WithName("CreateInventoryItem")
            .WithTags("Inventory Items")
            .WithSummary("Create a new inventory item")
            .Produces<CreateInventoryItemResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> CreateInventoryItem(
        CreateInventoryItemRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CreateInventoryItemCommand(
            Name: request.Name,
            Description: request.Description,
            Type: request.Type,
            Cost: request.Cost,
            Price: request.Price,
            StockQuantity: request.StockQuantity);

        var result = await mediator.Send(command, cancellationToken);

        var response = new CreateInventoryItemResponse(
            Id: result.Id,
            Name: result.Name,
            Description: result.Description,
            Type: result.Type,
            Cost: result.Cost,
            Price: result.Price,
            StockQuantity: result.StockQuantity,
            CreatedAt: result.CreatedAt);

        return Results.Created($"/inventory-items/{result.Id}", response);
    }
}
