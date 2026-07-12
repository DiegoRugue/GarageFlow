using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Tests.Shared.InventoryItems;
using Moq;

namespace GarageFlow.Tests.Unit.WorkOrders;

public sealed class EstimateDecisionProcessorTests
{
    [Fact]
    public async Task ReleaseAsync_ShouldRestoreStockInDeterministicInventoryItemOrder()
    {
        var firstItem = new InventoryItemBuilder().WithStockQuantity(5).Build();
        var secondItem = new InventoryItemBuilder().WithStockQuantity(10).Build();
        var orderedItems = new[] { firstItem, secondItem }.OrderBy(item => item.Id.Value).ToArray();
        var requestedIds = new List<InventoryItemId>();
        var inventoryItemRepository = new Mock<IInventoryItemRepository>();
        inventoryItemRepository
            .Setup(repository => repository.GetByIdForStockReservationAsync(
                It.IsAny<InventoryItemId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItemId id, CancellationToken _) =>
            {
                requestedIds.Add(id);
                return orderedItems.Single(item => item.Id == id);
            });
        var processor = new EstimateDecisionProcessor(inventoryItemRepository.Object);
        InventoryReservation[] reservations =
        [
            new(secondItem.Id, EstimateItemQuantity.Create(3)),
            new(firstItem.Id, EstimateItemQuantity.Create(2))
        ];

        await processor.ReleaseAsync(reservations, CancellationToken.None);

        Assert.Equal(orderedItems.Select(item => item.Id), requestedIds);
        Assert.Equal(7, firstItem.StockQuantity.Value);
        Assert.Equal(13, secondItem.StockQuantity.Value);
    }

    [Fact]
    public async Task ReleaseAsync_ShouldStopAndThrowNotFound_WhenInventoryItemDoesNotExist()
    {
        var missingItemId = InventoryItemId.From(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var laterItem = new InventoryItemBuilder().WithStockQuantity(10).Build();
        var requestedIds = new List<InventoryItemId>();
        var inventoryItemRepository = new Mock<IInventoryItemRepository>();
        inventoryItemRepository
            .Setup(repository => repository.GetByIdForStockReservationAsync(
                It.IsAny<InventoryItemId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItemId id, CancellationToken _) =>
            {
                requestedIds.Add(id);
                return id == laterItem.Id ? laterItem : null;
            });
        var processor = new EstimateDecisionProcessor(inventoryItemRepository.Object);
        InventoryReservation[] reservations =
        [
            new(laterItem.Id, EstimateItemQuantity.Create(3)),
            new(missingItemId, EstimateItemQuantity.Create(2))
        ];

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => processor.ReleaseAsync(reservations, CancellationToken.None));

        Assert.Equal($"Inventory item with ID '{missingItemId.Value}' was not found.", exception.Message);
        Assert.Equal([missingItemId], requestedIds);
        Assert.Equal(10, laterItem.StockQuantity.Value);
    }
}
