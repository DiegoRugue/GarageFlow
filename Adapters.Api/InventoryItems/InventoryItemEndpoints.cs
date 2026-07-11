using GarageFlow.Adapters.Api.InventoryItems.CreateInventoryItem;
using GarageFlow.Adapters.Api.InventoryItems.DeleteInventoryItem;
using GarageFlow.Adapters.Api.InventoryItems.GetInventoryItemById;
using GarageFlow.Adapters.Api.InventoryItems.ListInventoryItems;
using GarageFlow.Adapters.Api.InventoryItems.UpdateInventoryItem;
using GarageFlow.Adapters.Api.InventoryItems.UpdateInventoryItemStock;
using GarageFlow.Adapters.Api.Security;

namespace GarageFlow.Adapters.Api.InventoryItems;

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
