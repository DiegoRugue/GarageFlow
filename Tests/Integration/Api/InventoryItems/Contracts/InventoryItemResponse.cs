using System;

namespace GarageFlow.Tests.Integration.Api.InventoryItems.Contracts;

public sealed record InventoryItemResponse(
    Guid Id,
    string Name,
    string Description,
    string Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
