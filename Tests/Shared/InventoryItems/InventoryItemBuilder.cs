using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Domain.InventoryItems.ValueObjects;

namespace GarageFlow.Tests.Shared.InventoryItems;

public sealed class InventoryItemBuilder
{
    private string _name = "Oil Filter";
    private string _description = "Premium filter suitable for most engines.";
    private InventoryItemType _type = InventoryItemType.Part;
    private decimal _cost = 18.75m;
    private decimal _price = 39.90m;
    private int _stockQuantity = 25;

    public InventoryItemBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public InventoryItemBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public InventoryItemBuilder WithType(InventoryItemType type)
    {
        _type = type;
        return this;
    }

    public InventoryItemBuilder WithCost(decimal cost)
    {
        _cost = cost;
        return this;
    }

    public InventoryItemBuilder WithPrice(decimal price)
    {
        _price = price;
        return this;
    }

    public InventoryItemBuilder WithStockQuantity(int stockQuantity)
    {
        _stockQuantity = stockQuantity;
        return this;
    }

    public InventoryItem Build()
    {
        return InventoryItem.Create(
            InventoryItemName.Create(_name),
            Description.Create(_description),
            _type,
            Price.Create(_cost),
            Price.Create(_price),
            InventoryItemStockQuantity.Create(_stockQuantity));
    }

    public CreateInventoryItemRequest BuildCreateRequest()
    {
        return new CreateInventoryItemRequest(
            Name: _name,
            Description: _description,
            Type: (int)_type,
            Cost: _cost,
            Price: _price,
            StockQuantity: _stockQuantity);
    }

    public UpdateInventoryItemRequest BuildUpdateRequest()
    {
        return new UpdateInventoryItemRequest(
            Name: _name,
            Description: _description,
            Type: (int)_type,
            Cost: _cost,
            Price: _price);
    }

    public UpdateInventoryItemStockRequest BuildUpdateStockRequest()
    {
        return new UpdateInventoryItemStockRequest(StockQuantity: _stockQuantity);
    }
}
