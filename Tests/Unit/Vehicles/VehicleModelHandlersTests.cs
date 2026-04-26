using GarageFlow.Application.Vehicles.VehicleModels.CreateVehicleModel;
using GarageFlow.Application.Vehicles.VehicleModels.DeleteVehicleModel;
using GarageFlow.Application.Vehicles.VehicleModels.GetVehicleModelById;
using GarageFlow.Application.Vehicles.VehicleModels.ListVehicleModels;
using GarageFlow.Application.Vehicles.VehicleModels.UpdateVehicleModel;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using Mediator;
using Moq;

namespace GarageFlow.Tests.Unit.Vehicles;

public class VehicleModelHandlersTests
{
    [Fact]
    public async Task Handle_ShouldCreateVehicleModel_WhenDataIsValid()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModels = new List<VehicleModel>();
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock(vehicleModels);
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleBrandRepositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new CreateVehicleModelCommand(vehicleBrand.Id.Value, "  Uno  "), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(vehicleBrand.Id.Value, result.VehicleBrandId);
        Assert.Equal("Uno", result.Name);
        Assert.Single(vehicleModels);
        Assert.Equal("Uno", vehicleModels[0].Name);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreateVehicleModel_WhenNameExistsInDifferentBrand()
    {
        var firstBrand = VehicleBrand.Create("Fiat");
        var secondBrand = VehicleBrand.Create("Ford");
        var existingModel = VehicleModel.Create(firstBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([existingModel]);
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([firstBrand, secondBrand]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleBrandRepositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new CreateVehicleModelCommand(secondBrand.Id.Value, "Uno"), CancellationToken.None);

        Assert.Equal(secondBrand.Id.Value, result.VehicleBrandId);
        Assert.Equal("Uno", result.Name);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenCreatingWithMissingBrand()
    {
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleBrandRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new CreateVehicleModelCommand(Guid.NewGuid(), "Uno"), CancellationToken.None));

        vehicleModelRepositoryMock.Verify(
            x => x.ExistsByNameAsync(It.IsAny<VehicleBrandId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenCreatingWithEmptyVehicleBrandId()
    {
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleBrandRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new CreateVehicleModelCommand(Guid.Empty, "Uno"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenModelNameAlreadyExistsOnCreate_WithWhitespaceNormalization()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var existingModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([existingModel]);
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleBrandRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new CreateVehicleModelCommand(vehicleBrand.Id.Value, "  Uno  "), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackCreate_WhenExceptionOccursAfterTransactionBegins()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        vehicleModelRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<VehicleModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Add failed"));

        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleBrandRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new CreateVehicleModelCommand(vehicleBrand.Id.Value, "Uno"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnVehicleModel_WhenIdExists()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var handler = new GetVehicleModelByIdHandler(vehicleModelRepositoryMock.Object);

        var result = await handler.Handle(new GetVehicleModelByIdQuery(vehicleModel.Id.Value), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(vehicleModel.Id.Value, result.Id);
        Assert.Equal(vehicleBrand.Id.Value, result.VehicleBrandId);
        Assert.Equal("Uno", result.Name);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenVehicleModelDoesNotExist()
    {
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var handler = new GetVehicleModelByIdHandler(vehicleModelRepositoryMock.Object);

        var result = await handler.Handle(new GetVehicleModelByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ShouldListVehicleModels_WithPagination()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var firstModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var secondModel = VehicleModel.Create(vehicleBrand.Id, "Argo");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([firstModel, secondModel]);
        var handler = new ListVehicleModelsHandler(vehicleModelRepositoryMock.Object);

        var result = await handler.Handle(new ListVehicleModelsQuery(Page: 1, PageSize: 1), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(firstModel.Id.Value, result.Items[0].Id);
        Assert.Equal(firstModel.VehicleBrandId.Value, result.Items[0].VehicleBrandId);
        Assert.Equal("Uno", result.Items[0].Name);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPaginationBoundsAreInvalid()
    {
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var handler = new ListVehicleModelsHandler(vehicleModelRepositoryMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehicleModelsQuery(Page: 0, PageSize: 10), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehicleModelsQuery(Page: 1, PageSize: 0), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehicleModelsQuery(Page: 1, PageSize: 101), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldListVehicleModels_ByVehicleBrandId_WhenFiltering()
    {
        var firstBrand = VehicleBrand.Create("Fiat");
        var secondBrand = VehicleBrand.Create("Ford");
        var firstModel = VehicleModel.Create(firstBrand.Id, "Uno");
        var secondModel = VehicleModel.Create(firstBrand.Id, "Argo");
        var otherBrandModel = VehicleModel.Create(secondBrand.Id, "Ka");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([firstModel, secondModel, otherBrandModel]);
        var handler = new ListVehicleModelsHandler(vehicleModelRepositoryMock.Object);

        var result = await handler.Handle(
            new ListVehicleModelsQuery(Page: 1, PageSize: 10, VehicleBrandId: firstBrand.Id.Value),
            CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, model => Assert.Equal(firstBrand.Id.Value, model.VehicleBrandId));
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenVehicleBrandIdIsEmptyInListQuery()
    {
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var handler = new ListVehicleModelsHandler(vehicleModelRepositoryMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new ListVehicleModelsQuery(Page: 1, PageSize: 10, VehicleBrandId: Guid.Empty),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldUpdateVehicleModel_WhenDataIsValid()
    {
        var firstBrand = VehicleBrand.Create("Fiat");
        var secondBrand = VehicleBrand.Create("Ford");
        var vehicleModel = VehicleModel.Create(firstBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([firstBrand, secondBrand]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleModelHandler(
            vehicleModelRepositoryMock.Object,
            vehicleBrandRepositoryMock.Object,
            vehicleRepositoryMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(
            new UpdateVehicleModelCommand(vehicleModel.Id.Value, secondBrand.Id.Value, "  Fiesta  "),
            CancellationToken.None);

        Assert.Equal(vehicleModel.Id.Value, result.Id);
        Assert.Equal(secondBrand.Id.Value, result.VehicleBrandId);
        Assert.Equal("Fiesta", result.Name);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingMissingVehicleModel()
    {
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock();
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleModelHandler(
            vehicleModelRepositoryMock.Object,
            vehicleBrandRepositoryMock.Object,
            vehicleRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new UpdateVehicleModelCommand(Guid.NewGuid(), Guid.NewGuid(), "Fiesta"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenUpdatingWithEmptyVehicleBrandId()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleModelHandler(
            vehicleModelRepositoryMock.Object,
            vehicleBrandRepositoryMock.Object,
            vehicleRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new UpdateVehicleModelCommand(vehicleModel.Id.Value, Guid.Empty, "Fiesta"),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenMovingModelToBrandWithSameNormalizedName()
    {
        var sourceBrand = VehicleBrand.Create("Fiat");
        var targetBrand = VehicleBrand.Create("Ford");
        var sourceModel = VehicleModel.Create(sourceBrand.Id, "Uno");
        var existingTargetModel = VehicleModel.Create(targetBrand.Id, "Fiesta");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([sourceModel, existingTargetModel]);
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([sourceBrand, targetBrand]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleBrandRepositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new UpdateVehicleModelCommand(sourceModel.Id.Value, targetBrand.Id.Value, "  Fiesta  "),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingWithMissingBrand()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleBrandRepositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new UpdateVehicleModelCommand(vehicleModel.Id.Value, Guid.NewGuid(), "Fiesta"), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenModelNameAlreadyExistsOnUpdate_WithWhitespaceNormalization()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModelToUpdate = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var existingModel = VehicleModel.Create(vehicleBrand.Id, "Argo");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModelToUpdate, existingModel]);
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleBrandRepositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new UpdateVehicleModelCommand(vehicleModelToUpdate.Id.Value, vehicleBrand.Id.Value, "  Argo  "),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackUpdate_WhenExceptionOccursAfterTransactionBegins()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([vehicleBrand]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed"));

        var handler = new UpdateVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleBrandRepositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(
                new UpdateVehicleModelCommand(vehicleModel.Id.Value, vehicleBrand.Id.Value, "Fiesta"),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenChangingBrandOfModelWithRelatedVehicles()
    {
        var sourceBrand = VehicleBrand.Create("Fiat");
        var targetBrand = VehicleBrand.Create("Ford");
        var sourceModel = VehicleModel.Create(sourceBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([sourceModel]);
        var vehicleBrandRepositoryMock = CreateVehicleBrandRepositoryMock([sourceBrand, targetBrand]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock(existsByVehicleModelId: true);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleModelHandler(
            vehicleModelRepositoryMock.Object,
            vehicleBrandRepositoryMock.Object,
            vehicleRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new UpdateVehicleModelCommand(sourceModel.Id.Value, targetBrand.Id.Value, "Uno"),
                CancellationToken.None));

        vehicleRepositoryMock.Verify(
            x => x.ExistsByVehicleModelIdAsync(sourceModel.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDeleteVehicleModel_WhenIdExists()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await handler.Handle(new DeleteVehicleModelCommand(vehicleModel.Id.Value), CancellationToken.None);
        var deleted = await vehicleModelRepositoryMock.Object.GetByIdAsync(vehicleModel.Id, CancellationToken.None);

        Assert.Null(deleted);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        vehicleModelRepositoryMock.Verify(x => x.Remove(It.Is<VehicleModel>(m => m.Id == vehicleModel.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenDeletingMissingVehicleModel()
    {
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new DeleteVehicleModelCommand(Guid.NewGuid()), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        vehicleRepositoryMock.Verify(
            x => x.ExistsByVehicleModelIdAsync(It.IsAny<VehicleModelId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenDeletingVehicleModelWithVehicles()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock(existsByVehicleModelId: true);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new DeleteVehicleModelCommand(vehicleModel.Id.Value), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        vehicleModelRepositoryMock.Verify(x => x.Remove(It.IsAny<VehicleModel>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackDelete_WhenExceptionOccursAfterTransactionBegins()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        vehicleModelRepositoryMock
            .Setup(x => x.Remove(It.IsAny<VehicleModel>()))
            .Throws(new InvalidOperationException("Remove failed"));

        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleModelHandler(vehicleModelRepositoryMock.Object, vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new DeleteVehicleModelCommand(vehicleModel.Id.Value), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IVehicleModelRepository> CreateVehicleModelRepositoryMock(List<VehicleModel>? initialVehicleModels = null)
    {
        var vehicleModels = initialVehicleModels ?? [];
        var repositoryMock = new Mock<IVehicleModelRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<VehicleModelId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleModelId id, CancellationToken _) => vehicleModels.FirstOrDefault(model => model.Id == id));

        repositoryMock
            .Setup(x => x.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CancellationToken _) =>
            {
                var totalCount = vehicleModels.Count;
                var items = vehicleModels
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return ((IReadOnlyList<VehicleModel>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.ListByVehicleBrandIdAsync(
                It.IsAny<VehicleBrandId>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleBrandId vehicleBrandId, int page, int pageSize, CancellationToken _) =>
            {
                var filteredModels = vehicleModels
                    .Where(vehicleModel => vehicleModel.VehicleBrandId == vehicleBrandId)
                    .ToList();

                var totalCount = filteredModels.Count;
                var items = filteredModels
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return ((IReadOnlyList<VehicleModel>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<VehicleModel>(), It.IsAny<CancellationToken>()))
            .Returns((VehicleModel vehicleModel, CancellationToken _) =>
            {
                vehicleModels.Add(vehicleModel);
                return Task.CompletedTask;
            });

        repositoryMock
            .Setup(x => x.ExistsByNameAsync(It.IsAny<VehicleBrandId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleBrandId vehicleBrandId, string name, CancellationToken _) =>
                vehicleModels.Any(model =>
                    model.VehicleBrandId == vehicleBrandId &&
                    string.Equals(model.Name, name, StringComparison.OrdinalIgnoreCase)));

        repositoryMock
            .Setup(x => x.ExistsByNameAsync(It.IsAny<VehicleBrandId>(), It.IsAny<string>(), It.IsAny<VehicleModelId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleBrandId vehicleBrandId, string name, VehicleModelId excludingVehicleModelId, CancellationToken _) =>
                vehicleModels.Any(model =>
                    model.Id != excludingVehicleModelId &&
                    model.VehicleBrandId == vehicleBrandId &&
                    string.Equals(model.Name, name, StringComparison.OrdinalIgnoreCase)));

        repositoryMock
            .Setup(x => x.Remove(It.IsAny<VehicleModel>()))
            .Callback((VehicleModel vehicleModel) => vehicleModels.RemoveAll(model => model.Id == vehicleModel.Id));

        return repositoryMock;
    }

    private static Mock<IVehicleBrandRepository> CreateVehicleBrandRepositoryMock(List<VehicleBrand>? initialVehicleBrands = null)
    {
        var vehicleBrands = initialVehicleBrands ?? [];
        var repositoryMock = new Mock<IVehicleBrandRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<VehicleBrandId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleBrandId id, CancellationToken _) => vehicleBrands.FirstOrDefault(brand => brand.Id == id));

        return repositoryMock;
    }

    private static Mock<IVehicleRepository> CreateVehicleRepositoryMock(bool existsByVehicleModelId = false)
    {
        var repositoryMock = new Mock<IVehicleRepository>();

        repositoryMock
            .Setup(x => x.ExistsByVehicleModelIdAsync(It.IsAny<VehicleModelId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existsByVehicleModelId);

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
