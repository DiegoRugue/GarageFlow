using System;
using GarageFlow.Domain.InventoryItems.Enums;

namespace GarageFlow.Tests.Integration.Api.InventoryItems.Contracts;

public sealed record InventoryItemResponse(
    Guid Id,
    string Name,
    string Description,
    InventoryItemType Type,
    decimal Cost,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt);
