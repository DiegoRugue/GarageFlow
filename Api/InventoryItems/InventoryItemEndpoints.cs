using GarageFlow.Api.InventoryItems.CreateInventoryItem;
using GarageFlow.Api.InventoryItems.DeleteInventoryItem;
using GarageFlow.Api.InventoryItems.GetInventoryItemById;
using GarageFlow.Api.InventoryItems.ListInventoryItems;
using GarageFlow.Api.InventoryItems.UpdateInventoryItem;
using GarageFlow.Api.InventoryItems.UpdateInventoryItemStock;
using GarageFlow.Api.Security;

namespace GarageFlow.Api.InventoryItems;

public static class InventoryItemEndpoints
{
    public static IEndpointRouteBuilder MapInventoryItemEndpoints(this IEndpointRouteBuilder app)
    {
        var protectedInventoryItemRoutes = app.MapGroup(string.Empty)
            .RequireAuthorization(SecurityPolicies.ActiveAttendant);

        protectedInventoryItemRoutes.MapCreateInventoryItemEndpoint();
        protectedInventoryItemRoutes.MapGetInventoryItemByIdEndpoint();
        protectedInventoryItemRoutes.MapListInventoryItemsEndpoint();
        protectedInventoryItemRoutes.MapUpdateInventoryItemEndpoint();
        protectedInventoryItemRoutes.MapDeleteInventoryItemEndpoint();
        protectedInventoryItemRoutes.MapUpdateInventoryItemStockEndpoint();

        return app;
    }
}
