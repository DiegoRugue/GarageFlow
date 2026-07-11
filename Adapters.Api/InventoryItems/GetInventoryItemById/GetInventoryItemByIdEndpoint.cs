using GarageFlow.Application.InventoryItems.UseCases.GetInventoryItemById;
using Mediator;

namespace GarageFlow.Adapters.Api.InventoryItems.GetInventoryItemById;

public static class GetInventoryItemByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetInventoryItemByIdEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/inventory-items/{id:guid}", GetInventoryItemById)
            .WithName("GetInventoryItemById")
            .WithTags("Inventory Items")
            .WithSummary("Get an inventory item by its unique identifier")
            .Produces<InventoryItemResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetInventoryItemById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetInventoryItemByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);

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
