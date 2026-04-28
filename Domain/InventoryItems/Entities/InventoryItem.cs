using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.Interfaces;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Domain.InventoryItems.Events;
using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Domain.InventoryItems.Entities;

public sealed class InventoryItem : Entity<InventoryItemId>, IAggregateRoot
{
    public InventoryItemName Name { get; private set; }
    public Description Description { get; private set; }
    public InventoryItemType Type { get; private set; }
    public Price Cost { get; private set; }
    public Price Price { get; private set; }
    public InventoryItemStockQuantity StockQuantity { get; private set; }

    private InventoryItem(InventoryItemId id) : base(id)
    {
        Name = null!;
        Description = null!;
        Cost = null!;
        Price = null!;
    }

    private InventoryItem(
        InventoryItemId id,
        InventoryItemName name,
        Description description,
        InventoryItemType type,
        Price cost,
        Price price,
        InventoryItemStockQuantity stockQuantity) : base(id)
    {
        Name = name;
        Description = description;
        Type = type;
        Cost = cost;
        Price = price;
        StockQuantity = stockQuantity;
    }

    public static InventoryItem Create(
        InventoryItemName name,
        Description description,
        InventoryItemType type,
        Price cost,
        Price price,
        InventoryItemStockQuantity stockQuantity)
    {
        var id = InventoryItemId.New();
        var validatedName = EnsureValidName(name);
        var validatedType = EnsureValidType(type);
        var validatedDescription = EnsureValidDescription(description);
        var validatedCost = EnsureValidPrice(cost);
        var validatedPrice = EnsureValidPrice(price);
        var validatedStockQuantity = stockQuantity;

        var inventoryItem = new InventoryItem(
            id,
            validatedName,
            validatedDescription,
            validatedType,
            validatedCost,
            validatedPrice,
            validatedStockQuantity);

        inventoryItem.RaiseDomainEvent(new InventoryItemCreated(
            InventoryItemId: id,
            Name: validatedName.Value,
            Description: validatedDescription.Value,
            Type: validatedType,
            Cost: validatedCost.Value,
            Price: validatedPrice.Value,
            StockQuantity: validatedStockQuantity.Value,
            CreatedAt: inventoryItem.CreatedAt));

        return inventoryItem;
    }

    public void Update(
        InventoryItemName name,
        Description description,
        InventoryItemType type,
        Price cost,
        Price price)
    {
        Name = EnsureValidName(name);
        Description = EnsureValidDescription(description);
        Type = EnsureValidType(type);
        Cost = EnsureValidPrice(cost);
        Price = EnsureValidPrice(price);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new InventoryItemUpdated(
            InventoryItemId: Id,
            Name: Name.Value,
            Description: Description.Value,
            Type: Type,
            Cost: Cost.Value,
            Price: Price.Value));
    }

    public void SetStockQuantity(InventoryItemStockQuantity stockQuantity)
    {
        var validatedStockQuantity = stockQuantity;
        var previousStockQuantity = StockQuantity;
        StockQuantity = validatedStockQuantity;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new InventoryItemStockUpdated(
            InventoryItemId: Id,
            PreviousStockQuantity: previousStockQuantity.Value,
            NewStockQuantity: validatedStockQuantity.Value));
    }

    public void Delete()
    {
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new InventoryItemDeleted(
            InventoryItemId: Id,
            Name: Name.Value));
    }

    private static InventoryItemName EnsureValidName(InventoryItemName name)
    {
        if (name is null)
        {
            throw new ValidationException("Inventory item name cannot be null.");
        }

        return name;
    }

    private static InventoryItemType EnsureValidType(InventoryItemType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ValidationException("Inventory item type is invalid.");
        }

        return type;
    }

    private static Description EnsureValidDescription(Description description)
    {
        if (description is null)
        {
            throw new ValidationException("Inventory item description cannot be null.");
        }

        return description;
    }

    private static Price EnsureValidPrice(Price price)
    {
        if (price is null)
        {
            throw new ValidationException("Inventory item price cannot be null.");
        }

        return price;
    }
}
