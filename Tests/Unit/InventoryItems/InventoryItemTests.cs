using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Domain.InventoryItems.Events;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Tests.Shared.InventoryItems;

namespace GarageFlow.Tests.Unit.InventoryItems;

public class InventoryItemTests
{
    [Fact]
    public void Create_ShouldRaiseInventoryItemCreatedEvent()
    {
        var item = new InventoryItemBuilder()
            .WithName("Brake Pads")
            .WithDescription("High-quality ceramic brake pads.")
            .WithType(InventoryItemType.Part)
            .WithCost(25.99m)
            .WithPrice(49.99m)
            .WithStockQuantity(50)
            .Build();

        var createdEvent = Assert.Single(item.DomainEvents.OfType<InventoryItemCreated>());
        Assert.Equal(item.Id, createdEvent.InventoryItemId);
        Assert.Equal(item.Name.Value, createdEvent.Name);
        Assert.Equal(item.Description.Value, createdEvent.Description);
        Assert.Equal(item.Type, createdEvent.Type);
        Assert.Equal(item.Cost.Value, createdEvent.Cost);
        Assert.Equal(item.Price.Value, createdEvent.Price);
        Assert.Equal(item.StockQuantity.Value, createdEvent.StockQuantity);
    }

    [Fact]
    public void Update_ShouldRaiseInventoryItemUpdatedEvent()
    {
        var item = new InventoryItemBuilder().Build();

        item.Update(
            InventoryItemName.Create("Ceramic Filter"),
            Description.Create("High-filtration replacement filter."),
            InventoryItemType.Supply,
            Price.Create(24.40m),
            Price.Create(39.90m));

        var updatedEvent = Assert.Single(item.DomainEvents.OfType<InventoryItemUpdated>());
        Assert.Equal(item.Id, updatedEvent.InventoryItemId);
        Assert.Equal("Ceramic Filter", updatedEvent.Name);
        Assert.Equal("High-filtration replacement filter.", updatedEvent.Description);
        Assert.Equal(InventoryItemType.Supply, updatedEvent.Type);
        Assert.Equal(24.40m, updatedEvent.Cost);
        Assert.Equal(39.90m, updatedEvent.Price);
        Assert.Equal("Ceramic Filter", item.Name.Value);
        Assert.Equal("High-filtration replacement filter.", item.Description.Value);
        Assert.Equal(InventoryItemType.Supply, item.Type);
        Assert.Equal(24.40m, item.Cost.Value);
        Assert.Equal(39.90m, item.Price.Value);
    }

    [Fact]
    public void Delete_ShouldRaiseInventoryItemDeletedEvent()
    {
        var item = new InventoryItemBuilder().Build();

        item.Delete();

        var deletedEvent = Assert.Single(item.DomainEvents.OfType<InventoryItemDeleted>());
        Assert.Equal(item.Id, deletedEvent.InventoryItemId);
        Assert.Equal(item.Name.Value, deletedEvent.Name);
    }

    [Fact]
    public void SetStockQuantity_ShouldRaiseInventoryItemStockUpdatedEvent()
    {
        var item = new InventoryItemBuilder().WithStockQuantity(10).Build();

        item.SetStockQuantity(InventoryItemStockQuantity.Create(25));

        var stockUpdatedEvent = Assert.Single(item.DomainEvents.OfType<InventoryItemStockUpdated>());
        Assert.Equal(item.Id, stockUpdatedEvent.InventoryItemId);
        Assert.Equal(10, stockUpdatedEvent.PreviousStockQuantity);
        Assert.Equal(25, stockUpdatedEvent.NewStockQuantity);
        Assert.Equal(25, item.StockQuantity.Value);
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenDescriptionIsNull()
    {
        var cost = Price.Create(10m);
        var price = Price.Create(15m);

        Assert.Throws<ValidationException>(() => InventoryItem.Create(
            InventoryItemName.Create("Brake Pads"),
            null!,
            InventoryItemType.Part,
            cost,
            price,
            InventoryItemStockQuantity.Create(1)));
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenCostIsNull()
    {
        Assert.Throws<ValidationException>(() => InventoryItem.Create(
            InventoryItemName.Create("Brake Pads"),
            Description.Create("Valid description"),
            InventoryItemType.Part,
            null!,
            Price.Create(15m),
            InventoryItemStockQuantity.Create(1)));
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenPriceIsNull()
    {
        Assert.Throws<ValidationException>(() => InventoryItem.Create(
            InventoryItemName.Create("Brake Pads"),
            Description.Create("Valid description"),
            InventoryItemType.Part,
            Price.Create(10m),
            null!,
            InventoryItemStockQuantity.Create(1)));
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenTypeIsInvalid()
    {
        var description = Description.Create("Valid description");
        var cost = Price.Create(10m);
        var price = Price.Create(15m);

        Assert.Throws<ValidationException>(() => InventoryItem.Create(
            InventoryItemName.Create("Brake Pads"),
            description,
            (InventoryItemType)999,
            cost,
            price,
            InventoryItemStockQuantity.Create(1)));
    }

    [Fact]
    public void Update_ShouldThrowValidationException_WhenTypeIsInvalid()
    {
        var item = new InventoryItemBuilder().Build();
        var description = Description.Create(item.Description.Value);
        var cost = Price.Create(15m);
        var price = Price.Create(20m);
        var originalName = item.Name.Value;
        var originalDescription = item.Description.Value;
        var originalType = item.Type;
        var originalCost = item.Cost.Value;
        var originalPrice = item.Price.Value;
        var originalStockQuantity = item.StockQuantity.Value;

        Assert.Throws<ValidationException>(() => item.Update(
            item.Name,
            description,
            (InventoryItemType)999,
            cost,
            price));
        Assert.Equal(originalName, item.Name.Value);
        Assert.Equal(originalDescription, item.Description.Value);
        Assert.Equal(originalType, item.Type);
        Assert.Equal(originalCost, item.Cost.Value);
        Assert.Equal(originalPrice, item.Price.Value);
        Assert.Equal(originalStockQuantity, item.StockQuantity.Value);
        Assert.Empty(item.DomainEvents.OfType<InventoryItemUpdated>());
    }

    [Fact]
    public void Update_ShouldThrowValidationException_WhenDescriptionIsNull()
    {
        var item = new InventoryItemBuilder().Build();
        var cost = Price.Create(15m);
        var price = Price.Create(20m);
        var originalName = item.Name.Value;
        var originalDescription = item.Description.Value;
        var originalType = item.Type;
        var originalCost = item.Cost.Value;
        var originalPrice = item.Price.Value;
        var originalStockQuantity = item.StockQuantity.Value;

        Assert.Throws<ValidationException>(() => item.Update(
            item.Name,
            null!,
            InventoryItemType.Part,
            cost,
            price));
        Assert.Equal(originalName, item.Name.Value);
        Assert.Equal(originalDescription, item.Description.Value);
        Assert.Equal(originalType, item.Type);
        Assert.Equal(originalCost, item.Cost.Value);
        Assert.Equal(originalPrice, item.Price.Value);
        Assert.Equal(originalStockQuantity, item.StockQuantity.Value);
        Assert.Empty(item.DomainEvents.OfType<InventoryItemUpdated>());
    }

    [Fact]
    public void Update_ShouldThrowValidationException_WhenCostIsNull()
    {
        var item = new InventoryItemBuilder().Build();
        var originalName = item.Name.Value;
        var originalDescription = item.Description.Value;
        var originalType = item.Type;
        var originalCost = item.Cost.Value;
        var originalPrice = item.Price.Value;
        var originalStockQuantity = item.StockQuantity.Value;

        Assert.Throws<ValidationException>(() => item.Update(
            item.Name,
            Description.Create(originalDescription),
            InventoryItemType.Part,
            null!,
            Price.Create(originalPrice)));
        Assert.Equal(originalName, item.Name.Value);
        Assert.Equal(originalDescription, item.Description.Value);
        Assert.Equal(originalType, item.Type);
        Assert.Equal(originalCost, item.Cost.Value);
        Assert.Equal(originalPrice, item.Price.Value);
        Assert.Equal(originalStockQuantity, item.StockQuantity.Value);
        Assert.Empty(item.DomainEvents.OfType<InventoryItemUpdated>());
    }

    [Fact]
    public void Update_ShouldThrowValidationException_WhenPriceIsNull()
    {
        var item = new InventoryItemBuilder().Build();
        var originalName = item.Name.Value;
        var originalDescription = item.Description.Value;
        var originalType = item.Type;
        var originalCost = item.Cost.Value;
        var originalPrice = item.Price.Value;
        var originalStockQuantity = item.StockQuantity.Value;

        Assert.Throws<ValidationException>(() => item.Update(
            item.Name,
            Description.Create(originalDescription),
            InventoryItemType.Part,
            Price.Create(originalCost),
            null!));
        Assert.Equal(originalName, item.Name.Value);
        Assert.Equal(originalDescription, item.Description.Value);
        Assert.Equal(originalType, item.Type);
        Assert.Equal(originalCost, item.Cost.Value);
        Assert.Equal(originalPrice, item.Price.Value);
        Assert.Equal(originalStockQuantity, item.StockQuantity.Value);
        Assert.Empty(item.DomainEvents.OfType<InventoryItemUpdated>());
    }
}
