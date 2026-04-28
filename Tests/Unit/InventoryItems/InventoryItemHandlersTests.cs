using GarageFlow.Application.InventoryItems.CreateInventoryItem;
using GarageFlow.Application.InventoryItems.DeleteInventoryItem;
using GarageFlow.Application.InventoryItems.GetInventoryItemById;
using GarageFlow.Application.InventoryItems.ListInventoryItems;
using GarageFlow.Application.InventoryItems.UpdateInventoryItem;
using GarageFlow.Application.InventoryItems.UpdateInventoryItemStock;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.Enums;
using GarageFlow.Domain.InventoryItems.Repositories;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Tests.Shared.InventoryItems;
using Mediator;
using Moq;

namespace GarageFlow.Tests.Unit.InventoryItems;

public class InventoryItemHandlersTests
{
    [Fact]
    public async Task Handle_ShouldCreateInventoryItem_WhenDataIsValid()
    {
        var inventoryItems = new List<InventoryItem>();
        var repositoryMock = CreateInventoryItemRepositoryMock(inventoryItems);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        var command = new CreateInventoryItemCommand(
            Name: "Brake Pads",
            Description: "High-quality ceramic brake pad set.",
            Type: InventoryItemType.Part,
            Cost: 29.90m,
            Price: 59.80m,
            StockQuantity: 12);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Brake Pads", result.Name);
        Assert.Equal("High-quality ceramic brake pad set.", result.Description);
        Assert.Equal(InventoryItemType.Part, result.Type);
        Assert.Equal(29.90m, result.Cost);
        Assert.Equal(59.80m, result.Price);
        Assert.Equal(12, result.StockQuantity);
        Assert.Single(inventoryItems);
        Assert.Equal("Brake Pads", inventoryItems[0].Name.Value);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackCreate_WhenExceptionOccursAfterTransactionBegins()
    {
        var repositoryMock = CreateInventoryItemRepositoryMock();
        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Add failed"));

        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);
        var command = new CreateInventoryItemCommand(
            Name: "Brake Pads",
            Description: "High-quality ceramic brake pad set.",
            Type: InventoryItemType.Part,
            Cost: 29.90m,
            Price: 59.80m,
            StockQuantity: 12);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(command, CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotBeginTransaction_WhenCreateDescriptionIsInvalid()
    {
        var repositoryMock = CreateInventoryItemRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new CreateInventoryItemCommand(
                    Name: "Brake Pads",
                    Description: "   ",
                    Type: InventoryItemType.Part,
                    Cost: 29.90m,
                    Price: 59.80m,
                    StockQuantity: 12),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotBeginTransaction_WhenCreateCostIsInvalid()
    {
        var repositoryMock = CreateInventoryItemRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new CreateInventoryItemCommand(
                    Name: "Brake Pads",
                    Description: "High-quality ceramic brake pad set.",
                    Type: InventoryItemType.Part,
                    Cost: -1m,
                    Price: 59.80m,
                    StockQuantity: 12),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotBeginTransaction_WhenCreatePriceIsInvalid()
    {
        var repositoryMock = CreateInventoryItemRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new CreateInventoryItemCommand(
                    Name: "Brake Pads",
                    Description: "High-quality ceramic brake pad set.",
                    Type: InventoryItemType.Part,
                    Cost: 29.90m,
                    Price: 59.999m,
                    StockQuantity: 12),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnInventoryItem_WhenIdExists()
    {
        var item = new InventoryItemBuilder().Build();
        var repositoryMock = CreateInventoryItemRepositoryMock([item]);

        var handler = new GetInventoryItemByIdHandler(repositoryMock.Object);
        var query = new GetInventoryItemByIdQuery(item.Id.Value);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(item.Id.Value, result.Id);
        Assert.Equal(item.Name.Value, result.Name);
        Assert.Equal(item.Description.Value, result.Description);
        Assert.Equal(item.Type, result.Type);
        Assert.Equal(item.Cost.Value, result.Cost);
        Assert.Equal(item.Price.Value, result.Price);
        Assert.Equal(item.StockQuantity.Value, result.StockQuantity);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenInventoryItemDoesNotExist()
    {
        var repositoryMock = CreateInventoryItemRepositoryMock();
        var handler = new GetInventoryItemByIdHandler(repositoryMock.Object);

        var result = await handler.Handle(new GetInventoryItemByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldListInventoryItems_WithPagination()
    {
        var firstItem = new InventoryItemBuilder().Build();
        var secondItem = new InventoryItemBuilder()
            .WithName("Engine Oil")
            .WithDescription("Fully synthetic 5W30 synthetic oil.")
            .WithStockQuantity(100)
            .Build();

        var repositoryMock = CreateInventoryItemRepositoryMock([firstItem, secondItem]);
        var handler = new ListInventoryItemsHandler(repositoryMock.Object);

        var result = await handler.Handle(new ListInventoryItemsQuery(Page: 1, PageSize: 1), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(firstItem.Id.Value, result.Items[0].Id);
        Assert.Equal(firstItem.Name.Value, result.Items[0].Name);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPaginationBoundsAreInvalid()
    {
        var repositoryMock = CreateInventoryItemRepositoryMock();
        var handler = new ListInventoryItemsHandler(repositoryMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListInventoryItemsQuery(Page: 0, PageSize: 10), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListInventoryItemsQuery(Page: 1, PageSize: 0), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListInventoryItemsQuery(Page: 1, PageSize: 101), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldUpdateInventoryItem_WhenIdExists()
    {
        var item = new InventoryItemBuilder()
            .WithName("Brake Pads")
            .WithDescription("Ceramic set.")
            .WithType(InventoryItemType.Part)
            .WithCost(20m)
            .WithPrice(40m)
            .Build();

        var repositoryMock = CreateInventoryItemRepositoryMock([item]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(
            new UpdateInventoryItemCommand(
                Id: item.Id.Value,
                Name: "Synthetic Brake Pads",
                Description: "Ceramic set with low-dust formula.",
                Type: InventoryItemType.Supply,
                Cost: 30m,
                Price: 55m),
            CancellationToken.None);

        Assert.Equal(item.Id.Value, result.Id);
        Assert.Equal("Synthetic Brake Pads", result.Name);
        Assert.Equal("Ceramic set with low-dust formula.", result.Description);
        Assert.Equal(InventoryItemType.Supply, result.Type);
        Assert.Equal(30m, result.Cost);
        Assert.Equal(55m, result.Price);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingMissingInventoryItem()
    {
        var repositoryMock = CreateInventoryItemRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new UpdateInventoryItemCommand(
                    Guid.NewGuid(),
                    "Synthetic Brake Pads",
                    "Ceramic set.",
                    InventoryItemType.Supply,
                    30m,
                    55m),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackUpdate_WhenExceptionOccursAfterTransactionBegins()
    {
        var item = new InventoryItemBuilder().Build();
        var repositoryMock = CreateInventoryItemRepositoryMock([item]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed"));

        var handler = new UpdateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(
                new UpdateInventoryItemCommand(
                    item.Id.Value,
                    "Synthetic Brake Pads",
                    "Ceramic set.",
                    InventoryItemType.Supply,
                    30m,
                    55m),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotBeginTransaction_WhenUpdateDescriptionIsInvalid()
    {
        var item = new InventoryItemBuilder().Build();
        var repositoryMock = CreateInventoryItemRepositoryMock([item]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new UpdateInventoryItemCommand(
                    item.Id.Value,
                    "Synthetic Brake Pads",
                    "   ",
                    InventoryItemType.Supply,
                    30m,
                    55m),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotBeginTransaction_WhenUpdateCostIsInvalid()
    {
        var item = new InventoryItemBuilder().Build();
        var repositoryMock = CreateInventoryItemRepositoryMock([item]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new UpdateInventoryItemCommand(
                    item.Id.Value,
                    "Synthetic Brake Pads",
                    "Ceramic set.",
                    InventoryItemType.Supply,
                    -1m,
                    55m),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotBeginTransaction_WhenUpdatePriceIsInvalid()
    {
        var item = new InventoryItemBuilder().Build();
        var repositoryMock = CreateInventoryItemRepositoryMock([item]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new UpdateInventoryItemCommand(
                    item.Id.Value,
                    "Synthetic Brake Pads",
                    "Ceramic set.",
                    InventoryItemType.Supply,
                    30m,
                    55.999m),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDeleteInventoryItem_WhenIdExists()
    {
        var item = new InventoryItemBuilder().Build();
        var repositoryMock = CreateInventoryItemRepositoryMock([item]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await handler.Handle(new DeleteInventoryItemCommand(item.Id.Value), CancellationToken.None);

        var deleted = await repositoryMock.Object.GetByIdAsync(item.Id, CancellationToken.None);

        Assert.Null(deleted);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(x => x.Remove(It.Is<InventoryItem>(i => i.Id == item.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenDeletingMissingInventoryItem()
    {
        var repositoryMock = CreateInventoryItemRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new DeleteInventoryItemCommand(Guid.NewGuid()), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(x => x.Remove(It.IsAny<InventoryItem>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackDelete_WhenExceptionOccursAfterTransactionBegins()
    {
        var item = new InventoryItemBuilder().Build();
        var repositoryMock = CreateInventoryItemRepositoryMock([item]);
        repositoryMock
            .Setup(x => x.Remove(It.IsAny<InventoryItem>()))
            .Throws(new InvalidOperationException("Remove failed"));

        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteInventoryItemHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new DeleteInventoryItemCommand(item.Id.Value), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUpdateInventoryItemStock_WhenDataIsValid()
    {
        var item = new InventoryItemBuilder().WithStockQuantity(15).Build();
        var repositoryMock = CreateInventoryItemRepositoryMock([item]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateInventoryItemStockHandler(repositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new UpdateInventoryItemStockCommand(item.Id.Value, 42), CancellationToken.None);

        Assert.Equal(item.Id.Value, result.Id);
        Assert.Equal(42, result.StockQuantity);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingStockForMissingInventoryItem()
    {
        var repositoryMock = CreateInventoryItemRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateInventoryItemStockHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new UpdateInventoryItemStockCommand(Guid.NewGuid(), 10),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotBeginTransaction_WhenUpdateStockIsInvalid()
    {
        var item = new InventoryItemBuilder().Build();
        var repositoryMock = CreateInventoryItemRepositoryMock([item]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateInventoryItemStockHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new UpdateInventoryItemStockCommand(item.Id.Value, -5), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackUpdateStock_WhenCommitFailsAfterTransactionBegins()
    {
        var item = new InventoryItemBuilder().WithStockQuantity(15).Build();
        var repositoryMock = CreateInventoryItemRepositoryMock([item]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed"));

        var handler = new UpdateInventoryItemStockHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new UpdateInventoryItemStockCommand(item.Id.Value, 33), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IInventoryItemRepository> CreateInventoryItemRepositoryMock(List<InventoryItem>? initialInventoryItems = null)
    {
        var inventoryItems = initialInventoryItems ?? [];
        var repositoryMock = new Mock<IInventoryItemRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItemId id, CancellationToken _) => inventoryItems.FirstOrDefault(i => i.Id == id));

        repositoryMock
            .Setup(x => x.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CancellationToken _) =>
            {
                var totalCount = inventoryItems.Count;
                var items = inventoryItems
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return ((IReadOnlyList<InventoryItem>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
            .Returns((InventoryItem item, CancellationToken _) =>
            {
                inventoryItems.Add(item);
                return Task.CompletedTask;
            });

        repositoryMock
            .Setup(x => x.Remove(It.IsAny<InventoryItem>()))
            .Callback((InventoryItem item) => inventoryItems.RemoveAll(i => i.Id == item.Id));

        return repositoryMock;
    }

    private static Mock<IUnitOfWork> CreateUnitOfWorkMock()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();

        unitOfWorkMock
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        return unitOfWorkMock;
    }
}
