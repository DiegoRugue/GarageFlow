using GarageFlow.Application.Vehicles.VehicleBrands.CreateVehicleBrand;
using GarageFlow.Application.Vehicles.VehicleBrands.DeleteVehicleBrand;
using GarageFlow.Application.Vehicles.VehicleBrands.GetVehicleBrandById;
using GarageFlow.Application.Vehicles.VehicleBrands.ListVehicleBrands;
using GarageFlow.Application.Vehicles.VehicleBrands.UpdateVehicleBrand;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;
using Moq;

namespace GarageFlow.Tests.Unit.Vehicles;

public class VehicleBrandHandlersTests
{
    [Fact]
    public async Task Handle_ShouldCreateVehicleBrand_WhenDataIsValid()
    {
        var vehicleBrands = new List<VehicleBrand>();
        var repositoryMock = CreateVehicleBrandRepositoryMock(vehicleBrands);
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new CreateVehicleBrandHandler(repositoryMock.Object, unitOfWorkMock.Object);
        var command = new CreateVehicleBrandCommand(Name: "Fiat");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Fiat", result.Name);
        Assert.Single(vehicleBrands);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenBrandNameAlreadyExistsOnCreate()
    {
        var existing = VehicleBrand.Create("Fiat");
        var repositoryMock = CreateVehicleBrandRepositoryMock([existing]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleBrandHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new CreateVehicleBrandCommand("Fiat"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenBrandNameAlreadyExistsOnCreate_WithWhitespaceNormalization()
    {
        var existing = VehicleBrand.Create("Fiat");
        var repositoryMock = CreateVehicleBrandRepositoryMock([existing]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleBrandHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new CreateVehicleBrandCommand("  Fiat  "), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackCreate_WhenExceptionOccursAfterTransactionBegins()
    {
        var repositoryMock = CreateVehicleBrandRepositoryMock();
        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<VehicleBrand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Add failed"));

        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleBrandHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new CreateVehicleBrandCommand("Fiat"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnVehicleBrand_WhenIdExists()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var repositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);

        var handler = new GetVehicleBrandByIdHandler(repositoryMock.Object);
        var query = new GetVehicleBrandByIdQuery(vehicleBrand.Id.Value);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(vehicleBrand.Id.Value, result.Id);
        Assert.Equal("Fiat", result.Name);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenVehicleBrandDoesNotExist()
    {
        var repositoryMock = CreateVehicleBrandRepositoryMock();

        var handler = new GetVehicleBrandByIdHandler(repositoryMock.Object);
        var query = new GetVehicleBrandByIdQuery(Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldListVehicleBrands_WithPagination()
    {
        var firstBrand = VehicleBrand.Create("Fiat");
        var secondBrand = VehicleBrand.Create("Ford");
        var repositoryMock = CreateVehicleBrandRepositoryMock([firstBrand, secondBrand]);

        var handler = new ListVehicleBrandsHandler(repositoryMock.Object);
        var query = new ListVehicleBrandsQuery(Page: 1, PageSize: 1);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(firstBrand.Id.Value, result.Items[0].Id);
        Assert.Equal("Fiat", result.Items[0].Name);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPaginationBoundsAreInvalid()
    {
        var repositoryMock = CreateVehicleBrandRepositoryMock();
        var handler = new ListVehicleBrandsHandler(repositoryMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehicleBrandsQuery(Page: 0, PageSize: 10), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehicleBrandsQuery(Page: 1, PageSize: 0), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehicleBrandsQuery(Page: 1, PageSize: 101), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldUpdateVehicleBrand_WhenIdExists()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var repositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new UpdateVehicleBrandHandler(repositoryMock.Object, unitOfWorkMock.Object);
        var command = new UpdateVehicleBrandCommand(Id: vehicleBrand.Id.Value, Name: "Ford");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(vehicleBrand.Id.Value, result.Id);
        Assert.Equal("Ford", result.Name);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingMissingVehicleBrand()
    {
        var repositoryMock = CreateVehicleBrandRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleBrandHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new UpdateVehicleBrandCommand(Guid.NewGuid(), "Ford"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenBrandNameAlreadyExistsOnUpdate()
    {
        var brandToUpdate = VehicleBrand.Create("Fiat");
        var existing = VehicleBrand.Create("Ford");
        var repositoryMock = CreateVehicleBrandRepositoryMock([brandToUpdate, existing]);
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new UpdateVehicleBrandHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new UpdateVehicleBrandCommand(brandToUpdate.Id.Value, "Ford"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenBrandNameAlreadyExistsOnUpdate_WithWhitespaceNormalization()
    {
        var brandToUpdate = VehicleBrand.Create("Fiat");
        var existing = VehicleBrand.Create("Ford");
        var repositoryMock = CreateVehicleBrandRepositoryMock([brandToUpdate, existing]);
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new UpdateVehicleBrandHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new UpdateVehicleBrandCommand(brandToUpdate.Id.Value, "  Ford  "), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackUpdate_WhenExceptionOccursAfterTransactionBegins()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var repositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed"));

        var handler = new UpdateVehicleBrandHandler(repositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new UpdateVehicleBrandCommand(vehicleBrand.Id.Value, "Ford"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldDeleteVehicleBrand_WhenIdExists()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var repositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new DeleteVehicleBrandHandler(repositoryMock.Object, vehicleModelRepositoryMock.Object, unitOfWorkMock.Object);
        var command = new DeleteVehicleBrandCommand(vehicleBrand.Id.Value);

        await handler.Handle(command, CancellationToken.None);
        var deleted = await repositoryMock.Object.GetByIdAsync(vehicleBrand.Id, CancellationToken.None);

        Assert.Null(deleted);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(x => x.Remove(It.Is<VehicleBrand>(v => v.Id == vehicleBrand.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenDeletingMissingVehicleBrand()
    {
        var repositoryMock = CreateVehicleBrandRepositoryMock();
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new DeleteVehicleBrandHandler(repositoryMock.Object, vehicleModelRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new DeleteVehicleBrandCommand(Guid.NewGuid()), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        vehicleModelRepositoryMock.Verify(x => x.ExistsByVehicleBrandIdAsync(It.IsAny<VehicleBrandId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenDeletingVehicleBrandWithModels()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var repositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock(existsByVehicleBrandId: true);
        var unitOfWorkMock = CreateUnitOfWorkMock();

        var handler = new DeleteVehicleBrandHandler(repositoryMock.Object, vehicleModelRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new DeleteVehicleBrandCommand(vehicleBrand.Id.Value), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(x => x.Remove(It.IsAny<VehicleBrand>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackDelete_WhenExceptionOccursAfterTransactionBegins()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var repositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        repositoryMock
            .Setup(x => x.Remove(It.IsAny<VehicleBrand>()))
            .Throws(new InvalidOperationException("Remove failed"));

        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleBrandHandler(repositoryMock.Object, vehicleModelRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new DeleteVehicleBrandCommand(vehicleBrand.Id.Value), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IVehicleBrandRepository> CreateVehicleBrandRepositoryMock(List<VehicleBrand>? initialVehicleBrands = null)
    {
        var vehicleBrands = initialVehicleBrands ?? [];
        var repositoryMock = new Mock<IVehicleBrandRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<VehicleBrandId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleBrandId id, CancellationToken _) => vehicleBrands.FirstOrDefault(v => v.Id == id));

        repositoryMock
            .Setup(x => x.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CancellationToken _) =>
            {
                var totalCount = vehicleBrands.Count;
                var items = vehicleBrands
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return ((IReadOnlyList<VehicleBrand>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<VehicleBrand>(), It.IsAny<CancellationToken>()))
            .Returns((VehicleBrand vehicleBrand, CancellationToken _) =>
            {
                vehicleBrands.Add(vehicleBrand);
                return Task.CompletedTask;
            });

        repositoryMock
            .Setup(x => x.ExistsByNameAsync(It.IsAny<VehicleBrandName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleBrandName name, CancellationToken _) =>
                vehicleBrands.Any(v => v.Name == name));

        repositoryMock
            .Setup(x => x.ExistsByNameAsync(It.IsAny<VehicleBrandName>(), It.IsAny<VehicleBrandId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleBrandName name, VehicleBrandId excludingVehicleBrandId, CancellationToken _) =>
                vehicleBrands.Any(v => v.Id != excludingVehicleBrandId && v.Name == name));

        repositoryMock
            .Setup(x => x.Remove(It.IsAny<VehicleBrand>()))
            .Callback((VehicleBrand vehicleBrand) => vehicleBrands.RemoveAll(v => v.Id == vehicleBrand.Id));

        return repositoryMock;
    }

    private static Mock<IVehicleModelRepository> CreateVehicleModelRepositoryMock(bool existsByVehicleBrandId = false)
    {
        var repositoryMock = new Mock<IVehicleModelRepository>();

        repositoryMock
            .Setup(x => x.ExistsByVehicleBrandIdAsync(It.IsAny<VehicleBrandId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existsByVehicleBrandId);

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
