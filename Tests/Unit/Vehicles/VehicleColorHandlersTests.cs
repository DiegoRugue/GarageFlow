using GarageFlow.Application.Vehicles.VehicleColors.CreateVehicleColor;
using GarageFlow.Application.Vehicles.VehicleColors.DeleteVehicleColor;
using GarageFlow.Application.Vehicles.VehicleColors.GetVehicleColorById;
using GarageFlow.Application.Vehicles.VehicleColors.ListVehicleColors;
using GarageFlow.Application.Vehicles.VehicleColors.UpdateVehicleColor;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;
using Moq;

namespace GarageFlow.Tests.Unit.Vehicles;

public class VehicleColorHandlersTests
{
    [Fact]
    public async Task Handle_ShouldCreateVehicleColor_WhenDataIsValid()
    {
        var vehicleColors = new List<VehicleColor>();
        var repositoryMock = CreateVehicleColorRepositoryMock(vehicleColors);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleColorHandler(repositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new CreateVehicleColorCommand(Name: "  Black  "), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Black", result.Name);
        Assert.Single(vehicleColors);
        Assert.Equal("Black", vehicleColors[0].Name);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenColorNameAlreadyExistsOnCreate()
    {
        var existing = VehicleColor.Create("Black");
        var repositoryMock = CreateVehicleColorRepositoryMock([existing]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleColorHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new CreateVehicleColorCommand("Black"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenColorNameAlreadyExistsOnCreate_WithWhitespaceNormalization()
    {
        var existing = VehicleColor.Create("Black");
        var repositoryMock = CreateVehicleColorRepositoryMock([existing]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleColorHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new CreateVehicleColorCommand("  Black  "), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackCreate_WhenExceptionOccursAfterTransactionBegins()
    {
        var repositoryMock = CreateVehicleColorRepositoryMock();
        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<VehicleColor>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Add failed"));

        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleColorHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new CreateVehicleColorCommand("Black"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnVehicleColor_WhenIdExists()
    {
        var vehicleColor = VehicleColor.Create("Black");
        var repositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var handler = new GetVehicleColorByIdHandler(repositoryMock.Object);

        var result = await handler.Handle(new GetVehicleColorByIdQuery(vehicleColor.Id.Value), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(vehicleColor.Id.Value, result.Id);
        Assert.Equal("Black", result.Name);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenVehicleColorDoesNotExist()
    {
        var repositoryMock = CreateVehicleColorRepositoryMock();
        var handler = new GetVehicleColorByIdHandler(repositoryMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new GetVehicleColorByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldListVehicleColors_WithPagination()
    {
        var firstColor = VehicleColor.Create("Black");
        var secondColor = VehicleColor.Create("White");
        var repositoryMock = CreateVehicleColorRepositoryMock([firstColor, secondColor]);
        var handler = new ListVehicleColorsHandler(repositoryMock.Object);

        var result = await handler.Handle(new ListVehicleColorsQuery(Page: 1, PageSize: 1), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(firstColor.Id.Value, result.Items[0].Id);
        Assert.Equal("Black", result.Items[0].Name);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPaginationBoundsAreInvalid()
    {
        var repositoryMock = CreateVehicleColorRepositoryMock();
        var handler = new ListVehicleColorsHandler(repositoryMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehicleColorsQuery(Page: 0, PageSize: 10), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehicleColorsQuery(Page: 1, PageSize: 0), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehicleColorsQuery(Page: 1, PageSize: 101), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldUpdateVehicleColor_WhenIdExists()
    {
        var vehicleColor = VehicleColor.Create("Black");
        var repositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleColorHandler(repositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new UpdateVehicleColorCommand(vehicleColor.Id.Value, "  White  "), CancellationToken.None);

        Assert.Equal(vehicleColor.Id.Value, result.Id);
        Assert.Equal("White", result.Name);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingMissingVehicleColor()
    {
        var repositoryMock = CreateVehicleColorRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleColorHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new UpdateVehicleColorCommand(Guid.NewGuid(), "White"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenColorNameAlreadyExistsOnUpdate()
    {
        var colorToUpdate = VehicleColor.Create("Black");
        var existing = VehicleColor.Create("White");
        var repositoryMock = CreateVehicleColorRepositoryMock([colorToUpdate, existing]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleColorHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new UpdateVehicleColorCommand(colorToUpdate.Id.Value, "White"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenColorNameAlreadyExistsOnUpdate_WithWhitespaceNormalization()
    {
        var colorToUpdate = VehicleColor.Create("Black");
        var existing = VehicleColor.Create("White");
        var repositoryMock = CreateVehicleColorRepositoryMock([colorToUpdate, existing]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleColorHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new UpdateVehicleColorCommand(colorToUpdate.Id.Value, "  White  "), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackUpdate_WhenExceptionOccursAfterTransactionBegins()
    {
        var vehicleColor = VehicleColor.Create("Black");
        var repositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed"));

        var handler = new UpdateVehicleColorHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new UpdateVehicleColorCommand(vehicleColor.Id.Value, "White"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldDeleteVehicleColor_WhenIdExists()
    {
        var vehicleColor = VehicleColor.Create("Black");
        var repositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleColorHandler(repositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await handler.Handle(new DeleteVehicleColorCommand(vehicleColor.Id.Value), CancellationToken.None);
        var deleted = await repositoryMock.Object.GetByIdAsync(vehicleColor.Id, CancellationToken.None);

        Assert.Null(deleted);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(x => x.Remove(It.Is<VehicleColor>(c => c.Id == vehicleColor.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenDeletingMissingVehicleColor()
    {
        var repositoryMock = CreateVehicleColorRepositoryMock();
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleColorHandler(repositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new DeleteVehicleColorCommand(Guid.NewGuid()), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        vehicleRepositoryMock.Verify(
            x => x.ExistsByVehicleColorIdAsync(It.IsAny<VehicleColorId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenDeletingVehicleColorWithVehicles()
    {
        var vehicleColor = VehicleColor.Create("Black");
        var repositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock(existsByVehicleColorId: true);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleColorHandler(repositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new DeleteVehicleColorCommand(vehicleColor.Id.Value), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(x => x.Remove(It.IsAny<VehicleColor>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackDelete_WhenExceptionOccursAfterTransactionBegins()
    {
        var vehicleColor = VehicleColor.Create("Black");
        var repositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        repositoryMock
            .Setup(x => x.Remove(It.IsAny<VehicleColor>()))
            .Throws(new InvalidOperationException("Remove failed"));

        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleColorHandler(repositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new DeleteVehicleColorCommand(vehicleColor.Id.Value), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IVehicleColorRepository> CreateVehicleColorRepositoryMock(List<VehicleColor>? initialVehicleColors = null)
    {
        var vehicleColors = initialVehicleColors ?? [];
        var repositoryMock = new Mock<IVehicleColorRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<VehicleColorId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleColorId id, CancellationToken _) => vehicleColors.FirstOrDefault(c => c.Id == id));

        repositoryMock
            .Setup(x => x.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CancellationToken _) =>
            {
                var totalCount = vehicleColors.Count;
                var items = vehicleColors
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return ((IReadOnlyList<VehicleColor>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<VehicleColor>(), It.IsAny<CancellationToken>()))
            .Returns((VehicleColor vehicleColor, CancellationToken _) =>
            {
                vehicleColors.Add(vehicleColor);
                return Task.CompletedTask;
            });

        repositoryMock
            .Setup(x => x.ExistsByNameAsync(It.IsAny<VehicleColorName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleColorName name, CancellationToken _) =>
                vehicleColors.Any(c => c.Name == name));

        repositoryMock
            .Setup(x => x.ExistsByNameAsync(It.IsAny<VehicleColorName>(), It.IsAny<VehicleColorId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleColorName name, VehicleColorId excludingVehicleColorId, CancellationToken _) =>
                vehicleColors.Any(c =>
                    c.Id != excludingVehicleColorId &&
                    c.Name == name));

        repositoryMock
            .Setup(x => x.Remove(It.IsAny<VehicleColor>()))
            .Callback((VehicleColor vehicleColor) => vehicleColors.RemoveAll(c => c.Id == vehicleColor.Id));

        return repositoryMock;
    }

    private static Mock<IVehicleRepository> CreateVehicleRepositoryMock(bool existsByVehicleColorId = false)
    {
        var repositoryMock = new Mock<IVehicleRepository>();

        repositoryMock
            .Setup(x => x.ExistsByVehicleColorIdAsync(It.IsAny<VehicleColorId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existsByVehicleColorId);

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
