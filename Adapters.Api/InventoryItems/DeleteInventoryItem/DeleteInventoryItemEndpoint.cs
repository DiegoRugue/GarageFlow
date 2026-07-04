using GarageFlow.Application.InventoryItems.UseCases.DeleteInventoryItem;
using Mediator;

namespace GarageFlow.Adapters.Api.InventoryItems.DeleteInventoryItem;

public static class DeleteInventoryItemEndpoint
{
    public static IEndpointRouteBuilder MapDeleteInventoryItemEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/inventory-items/{id:guid}", DeleteInventoryItem)
            .WithName("DeleteInventoryItem")
            .WithTags("Inventory Items")
            .WithSummary("Delete an existing inventory item by ID")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> DeleteInventoryItem(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeleteInventoryItemCommand(Id: id);
        await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }
}
