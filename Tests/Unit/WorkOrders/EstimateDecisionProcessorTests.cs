using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;
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
}
