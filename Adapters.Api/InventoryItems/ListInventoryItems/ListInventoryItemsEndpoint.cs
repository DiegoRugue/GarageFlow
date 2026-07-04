using GarageFlow.Adapters.Api.InventoryItems.GetInventoryItemById;
using GarageFlow.Application.InventoryItems.ListInventoryItems;
using Mediator;

namespace GarageFlow.Adapters.Api.InventoryItems.ListInventoryItems;

public static class ListInventoryItemsEndpoint
{
    public static IEndpointRouteBuilder MapListInventoryItemsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/inventory-items", ListInventoryItems)
            .WithName("ListInventoryItems")
            .WithTags("Inventory Items")
            .WithSummary("List inventory items with pagination")
            .Produces<ListInventoryItemsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ListInventoryItems(
        IMediator mediator,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var query = new ListInventoryItemsQuery(page, pageSize);
        var result = await mediator.Send(query, cancellationToken);

        var response = new ListInventoryItemsResponse(
            Items: result.Items
                .Select(dto => new InventoryItemResponse(
                    Id: dto.Id,
                    Name: dto.Name,
                    Description: dto.Description,
                    Type: dto.Type,
                    Cost: dto.Cost,
                    Price: dto.Price,
                    StockQuantity: dto.StockQuantity,
                    CreatedAt: dto.CreatedAt))
                .ToList(),
            TotalCount: result.TotalCount,
            Page: result.Page,
            PageSize: result.PageSize);

        return Results.Ok(response);
    }
}
