using GarageFlow.Application.Vehicles.CreateVehicle;
using GarageFlow.Application.Vehicles.DeleteVehicle;
using GarageFlow.Application.Vehicles.GetVehicleById;
using GarageFlow.Application.Vehicles.ListVehicles;
using GarageFlow.Application.Vehicles.UpdateVehicle;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Tests.Shared.Customers;
using Moq;

namespace GarageFlow.Tests.Unit.Vehicles;

public class VehicleHandlersTests
{
    [Fact]
    public async Task Handle_ShouldCreateVehicle_WhenDataIsValid()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var vehicles = new List<Vehicle>();

        var vehicleRepositoryMock = CreateVehicleRepositoryMock(vehicles);
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(
            new CreateVehicleCommand(
                Plate: "abc-1234",
                Year: 2024,
                CustomerId: customer.Id.Value,
                VehicleModelId: vehicleModel.Id.Value,
                VehicleColorId: vehicleColor.Id.Value),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(customer.Id.Value, result.CustomerId);
        Assert.Equal(2024, result.Year);
        Assert.Equal(vehicleBrand.Id.Value, result.VehicleBrandId);
        Assert.Equal(vehicleModel.Id.Value, result.VehicleModelId);
        Assert.Equal(vehicleColor.Id.Value, result.VehicleColorId);
        Assert.Equal("ABC1234", result.Plate);
        Assert.Single(vehicles);
        Assert.Equal(customer.Id, vehicles[0].CustomerId);
        Assert.Equal(2024, vehicles[0].Year);
        Assert.Equal("ABC1234", vehicles[0].LicensePlate.Value);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenCreatingWithMissingCustomer()
    {
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");

        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new CreateVehicleCommand("ABC1234", 2024, Guid.NewGuid(), vehicleModel.Id.Value, vehicleColor.Id.Value),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenCreatingWithMissingVehicleModel()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleColor = VehicleColor.Create("Black");

        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new CreateVehicleCommand("ABC1234", 2024, customer.Id.Value, Guid.NewGuid(), vehicleColor.Id.Value),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenCreatingWithMissingVehicleColor()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");

        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new CreateVehicleCommand("ABC1234", 2024, customer.Id.Value, vehicleModel.Id.Value, Guid.NewGuid()),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenPlateAlreadyExistsOnCreate()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var existingVehicle = Vehicle.Create(customer.Id, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));

        var vehicleRepositoryMock = CreateVehicleRepositoryMock([existingVehicle]);
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new CreateVehicleCommand("ABC1234", 2024, customer.Id.Value, vehicleModel.Id.Value, vehicleColor.Id.Value),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenNormalizedPlateAlreadyExistsOnCreate()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var existingVehicle = Vehicle.Create(customer.Id, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));

        var vehicleRepositoryMock = CreateVehicleRepositoryMock([existingVehicle]);
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new CreateVehicleCommand("abc-1234", 2024, customer.Id.Value, vehicleModel.Id.Value, vehicleColor.Id.Value),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackCreate_WhenExceptionOccursAfterTransactionBegins()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");

        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        vehicleRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Add failed"));

        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(
                new CreateVehicleCommand("ABC1234", 2024, customer.Id.Value, vehicleModel.Id.Value, vehicleColor.Id.Value),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnVehicle_WhenIdExists()
    {
        var customerId = CustomerId.New();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var vehicle = Vehicle.Create(customerId, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));
        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicle], vehicleBrands: [vehicleBrand], vehicleModels: [vehicleModel], vehicleColors: [vehicleColor]);
        var handler = new GetVehicleByIdHandler(vehicleRepositoryMock.Object);

        var result = await handler.Handle(new GetVehicleByIdQuery(vehicle.Id.Value), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(vehicle.Id.Value, result.Id);
        Assert.Equal(vehicle.CustomerId.Value, result.CustomerId);
        Assert.Equal(vehicle.Year.Value, result.Year);
        Assert.Equal(vehicle.VehicleBrandId.Value, result.VehicleBrandId);
        Assert.Equal("Fiat", result.VehicleBrandName);
        Assert.Equal(vehicle.VehicleModelId.Value, result.VehicleModelId);
        Assert.Equal("Uno", result.VehicleModelName);
        Assert.Equal(vehicle.VehicleColorId.Value, result.VehicleColorId);
        Assert.Equal("Black", result.VehicleColorName);
        Assert.Equal(vehicle.LicensePlate.Value, result.Plate);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenVehicleDoesNotExist()
    {
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var handler = new GetVehicleByIdHandler(vehicleRepositoryMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new GetVehicleByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldListVehicles_WithPagination()
    {
        var firstCustomerId = CustomerId.New();
        var secondCustomerId = CustomerId.New();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var firstVehicle = Vehicle.Create(firstCustomerId, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));
        var secondVehicle = Vehicle.Create(secondCustomerId, 2025, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("XYZ1A23"));
        var vehicleRepositoryMock = CreateVehicleRepositoryMock(
            [firstVehicle, secondVehicle],
            vehicleBrands: [vehicleBrand],
            vehicleModels: [vehicleModel],
            vehicleColors: [vehicleColor]);
        var handler = new ListVehiclesHandler(vehicleRepositoryMock.Object);

        var result = await handler.Handle(new ListVehiclesQuery(Page: 1, PageSize: 1), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(firstVehicle.Id.Value, result.Items[0].Id);
        Assert.Equal(firstVehicle.CustomerId.Value, result.Items[0].CustomerId);
        Assert.Equal(firstVehicle.Year.Value, result.Items[0].Year);
        Assert.Equal("Fiat", result.Items[0].VehicleBrandName);
        Assert.Equal("Uno", result.Items[0].VehicleModelName);
        Assert.Equal("Black", result.Items[0].VehicleColorName);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task Handle_ShouldFilterVehicles_ByCustomerId_WhenListing()
    {
        var firstCustomerId = CustomerId.New();
        var secondCustomerId = CustomerId.New();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var firstVehicle = Vehicle.Create(firstCustomerId, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));
        var secondVehicle = Vehicle.Create(secondCustomerId, 2025, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("XYZ1A23"));
        var vehicleRepositoryMock = CreateVehicleRepositoryMock(
            [firstVehicle, secondVehicle],
            vehicleBrands: [vehicleBrand],
            vehicleModels: [vehicleModel],
            vehicleColors: [vehicleColor]);
        var handler = new ListVehiclesHandler(vehicleRepositoryMock.Object);

        var result = await handler.Handle(
            new ListVehiclesQuery(Page: 1, PageSize: 10, CustomerId: firstCustomerId.Value),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(firstVehicle.Id.Value, result.Items[0].Id);
        Assert.Equal(firstVehicle.CustomerId.Value, result.Items[0].CustomerId);
        Assert.Equal(firstVehicle.Year.Value, result.Items[0].Year);
        Assert.Equal(firstCustomerId.Value, result.Items[0].CustomerId);
        Assert.Equal("Fiat", result.Items[0].VehicleBrandName);
        Assert.Equal("Uno", result.Items[0].VehicleModelName);
        Assert.Equal("Black", result.Items[0].VehicleColorName);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenCustomerIdIsEmptyInListQuery()
    {
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var handler = new ListVehiclesHandler(vehicleRepositoryMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new ListVehiclesQuery(Page: 1, PageSize: 10, CustomerId: Guid.Empty),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowValidationException_WhenPaginationBoundsAreInvalid()
    {
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var handler = new ListVehiclesHandler(vehicleRepositoryMock.Object);

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehiclesQuery(Page: 0, PageSize: 10), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehiclesQuery(Page: 1, PageSize: 0), CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new ListVehiclesQuery(Page: 1, PageSize: 101), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldUpdateVehicle_WhenDataIsValid()
    {
        var customer = new CustomerBuilder().Build();
        var firstBrand = VehicleBrand.Create("Fiat");
        var secondBrand = VehicleBrand.Create("Ford");
        var firstModel = VehicleModel.Create(firstBrand.Id, "Uno");
        var secondModel = VehicleModel.Create(secondBrand.Id, "Fiesta");
        var firstColor = VehicleColor.Create("Black");
        var secondColor = VehicleColor.Create("White");
        var vehicle = Vehicle.Create(customer.Id, 2024, firstBrand.Id, firstModel.Id, firstColor.Id, LicensePlate.Create("ABC1234"));

        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicle]);
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([firstModel, secondModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([firstColor, secondColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(
            new UpdateVehicleCommand(
                Id: vehicle.Id.Value,
                Plate: "xyz1a23",
                Year: 2025,
                CustomerId: customer.Id.Value,
                VehicleModelId: secondModel.Id.Value,
                VehicleColorId: secondColor.Id.Value),
            CancellationToken.None);

        Assert.Equal(vehicle.Id.Value, result.Id);
        Assert.Equal(customer.Id.Value, result.CustomerId);
        Assert.Equal(2025, result.Year);
        Assert.Equal(secondBrand.Id.Value, result.VehicleBrandId);
        Assert.Equal(secondModel.Id.Value, result.VehicleModelId);
        Assert.Equal(secondColor.Id.Value, result.VehicleColorId);
        Assert.Equal("XYZ1A23", result.Plate);
        Assert.Equal(customer.Id, vehicle.CustomerId);
        Assert.Equal(2025, vehicle.Year.Value);
        Assert.Equal(secondModel.VehicleBrandId, vehicle.VehicleBrandId);
        Assert.Equal(secondModel.Id, vehicle.VehicleModelId);
        Assert.Equal(secondColor.Id, vehicle.VehicleColorId);
        Assert.Equal("XYZ1A23", vehicle.LicensePlate.Value);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingMissingVehicle()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");

        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new UpdateVehicleCommand(Guid.NewGuid(), "ABC1234", 2024, customer.Id.Value, vehicleModel.Id.Value, vehicleColor.Id.Value),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingWithMissingCustomer()
    {
        var vehicleCustomerId = CustomerId.New();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var vehicle = Vehicle.Create(vehicleCustomerId, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));

        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicle]);
        var customerRepositoryMock = CreateCustomerRepositoryMock();
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new UpdateVehicleCommand(vehicle.Id.Value, "ABC1234", 2024, Guid.NewGuid(), vehicleModel.Id.Value, vehicleColor.Id.Value),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingWithMissingVehicleModel()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var vehicle = Vehicle.Create(customer.Id, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));

        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicle]);
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock();
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new UpdateVehicleCommand(vehicle.Id.Value, "ABC1234", 2024, customer.Id.Value, Guid.NewGuid(), vehicleColor.Id.Value),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUpdatingWithMissingVehicleColor()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var vehicle = Vehicle.Create(customer.Id, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));

        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicle]);
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new UpdateVehicleCommand(vehicle.Id.Value, "ABC1234", 2024, customer.Id.Value, vehicleModel.Id.Value, Guid.NewGuid()),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolationException_WhenNormalizedPlateAlreadyExistsOnUpdate()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var vehicleToUpdate = Vehicle.Create(customer.Id, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));
        var existingVehicle = Vehicle.Create(customer.Id, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("XYZ1A23"));

        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicleToUpdate, existingVehicle]);
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new UpdateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new UpdateVehicleCommand(
                    Id: vehicleToUpdate.Id.Value,
                    Plate: "  xyz-1a23  ",
                    Year: 2024,
                    CustomerId: customer.Id.Value,
                    VehicleModelId: vehicleModel.Id.Value,
                    VehicleColorId: vehicleColor.Id.Value),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackUpdate_WhenExceptionOccursAfterTransactionBegins()
    {
        var customer = new CustomerBuilder().Build();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var vehicle = Vehicle.Create(customer.Id, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));

        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicle]);
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleModelRepositoryMock = CreateVehicleModelRepositoryMock([vehicleModel]);
        var vehicleColorRepositoryMock = CreateVehicleColorRepositoryMock([vehicleColor]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed"));

        var handler = new UpdateVehicleHandler(
            vehicleRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleModelRepositoryMock.Object,
            vehicleColorRepositoryMock.Object,
            unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(
                new UpdateVehicleCommand(vehicle.Id.Value, "XYZ1A23", 2024, customer.Id.Value, vehicleModel.Id.Value, vehicleColor.Id.Value),
                CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldDeleteVehicle_WhenIdExists()
    {
        var customerId = CustomerId.New();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var vehicle = Vehicle.Create(customerId, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));
        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicle]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleHandler(vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await handler.Handle(new DeleteVehicleCommand(vehicle.Id.Value), CancellationToken.None);
        var deleted = await vehicleRepositoryMock.Object.GetByIdAsync(vehicle.Id, CancellationToken.None);

        Assert.Null(deleted);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        vehicleRepositoryMock.Verify(x => x.Remove(It.Is<Vehicle>(v => v.Id == vehicle.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenDeletingMissingVehicle()
    {
        var vehicleRepositoryMock = CreateVehicleRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleHandler(vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new DeleteVehicleCommand(Guid.NewGuid()), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRollbackDelete_WhenExceptionOccursAfterTransactionBegins()
    {
        var customerId = CustomerId.New();
        var vehicleBrand = VehicleBrand.Create("Fiat");
        var vehicleModel = VehicleModel.Create(vehicleBrand.Id, "Uno");
        var vehicleColor = VehicleColor.Create("Black");
        var vehicle = Vehicle.Create(customerId, 2024, vehicleBrand.Id, vehicleModel.Id, vehicleColor.Id, LicensePlate.Create("ABC1234"));
        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicle]);
        vehicleRepositoryMock
            .Setup(x => x.Remove(It.IsAny<Vehicle>()))
            .Throws(new InvalidOperationException("Remove failed"));

        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeleteVehicleHandler(vehicleRepositoryMock.Object, unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(new DeleteVehicleCommand(vehicle.Id.Value), CancellationToken.None));

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IVehicleRepository> CreateVehicleRepositoryMock(
        List<Vehicle>? initialVehicles = null,
        bool existsByCustomerId = false,
        List<VehicleBrand>? vehicleBrands = null,
        List<VehicleModel>? vehicleModels = null,
        List<VehicleColor>? vehicleColors = null)
    {
        var vehicles = initialVehicles ?? [];
        vehicleBrands ??= [];
        vehicleModels ??= [];
        vehicleColors ??= [];
        var repositoryMock = new Mock<IVehicleRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<VehicleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleId id, CancellationToken _) => vehicles.FirstOrDefault(v => v.Id == id));

        repositoryMock
            .Setup(x => x.GetDetailsByIdAsync(It.IsAny<VehicleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleId id, CancellationToken _) =>
            {
                var vehicle = vehicles.FirstOrDefault(v => v.Id == id);
                if (vehicle is null)
                {
                    return null;
                }

                return ToVehicleDetailsReadModel(vehicle, vehicleBrands, vehicleModels, vehicleColors);
            });

        repositoryMock
            .Setup(x => x.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CancellationToken _) =>
            {
                var totalCount = vehicles.Count;
                var items = vehicles
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return ((IReadOnlyList<Vehicle>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.ListDetailsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CustomerId?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CustomerId? customerId, CancellationToken _) =>
            {
                var filteredVehicles = vehicles
                    .Where(vehicle => customerId == null || vehicle.CustomerId == customerId)
                    .ToList();

                var totalCount = filteredVehicles.Count;
                var items = filteredVehicles
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(vehicle => ToVehicleDetailsReadModel(vehicle, vehicleBrands, vehicleModels, vehicleColors))
                    .ToList();

                return ((IReadOnlyList<VehicleDetailsReadModel>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.ListDetailsByCustomerIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerId customerId, CancellationToken _) =>
                vehicles
                    .Where(vehicle => vehicle.CustomerId == customerId)
                    .Select(vehicle => ToVehicleDetailsReadModel(vehicle, vehicleBrands, vehicleModels, vehicleColors))
                    .ToList());

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()))
            .Returns((Vehicle vehicle, CancellationToken _) =>
            {
                vehicles.Add(vehicle);
                return Task.CompletedTask;
            });

        repositoryMock
            .Setup(x => x.ExistsByLicensePlateAsync(It.IsAny<LicensePlate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LicensePlate licensePlate, CancellationToken _) =>
                vehicles.Any(v => string.Equals(v.LicensePlate.Value, licensePlate.Value, StringComparison.OrdinalIgnoreCase)));

        repositoryMock
            .Setup(x => x.ExistsByLicensePlateAsync(It.IsAny<LicensePlate>(), It.IsAny<VehicleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LicensePlate licensePlate, VehicleId excludingVehicleId, CancellationToken _) =>
                vehicles.Any(v =>
                    v.Id != excludingVehicleId &&
                    string.Equals(v.LicensePlate.Value, licensePlate.Value, StringComparison.OrdinalIgnoreCase)));

        repositoryMock
            .Setup(x => x.ExistsByVehicleBrandIdAsync(It.IsAny<VehicleBrandId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleBrandId vehicleBrandId, CancellationToken _) =>
                vehicles.Any(v => v.VehicleBrandId == vehicleBrandId));

        repositoryMock
            .Setup(x => x.ExistsByVehicleModelIdAsync(It.IsAny<VehicleModelId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleModelId vehicleModelId, CancellationToken _) =>
                vehicles.Any(v => v.VehicleModelId == vehicleModelId));

        repositoryMock
            .Setup(x => x.ExistsByVehicleColorIdAsync(It.IsAny<VehicleColorId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleColorId vehicleColorId, CancellationToken _) =>
                vehicles.Any(v => v.VehicleColorId == vehicleColorId));

        repositoryMock
            .Setup(x => x.ExistsByCustomerIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existsByCustomerId);

        repositoryMock
            .Setup(x => x.Remove(It.IsAny<Vehicle>()))
            .Callback((Vehicle vehicle) => vehicles.RemoveAll(v => v.Id == vehicle.Id));

        return repositoryMock;
    }

    private static VehicleDetailsReadModel ToVehicleDetailsReadModel(
        Vehicle vehicle,
        IReadOnlyList<VehicleBrand> vehicleBrands,
        IReadOnlyList<VehicleModel> vehicleModels,
        IReadOnlyList<VehicleColor> vehicleColors)
    {
        return new VehicleDetailsReadModel(
            Id: vehicle.Id.Value,
            CustomerId: vehicle.CustomerId.Value,
            Year: vehicle.Year.Value,
            VehicleBrandId: vehicle.VehicleBrandId.Value,
            VehicleBrandName: vehicleBrands.FirstOrDefault(vehicleBrand => vehicleBrand.Id == vehicle.VehicleBrandId)?.Name ?? string.Empty,
            VehicleModelId: vehicle.VehicleModelId.Value,
            VehicleModelName: vehicleModels.FirstOrDefault(vehicleModel => vehicleModel.Id == vehicle.VehicleModelId)?.Name ?? string.Empty,
            VehicleColorId: vehicle.VehicleColorId.Value,
            VehicleColorName: vehicleColors.FirstOrDefault(vehicleColor => vehicleColor.Id == vehicle.VehicleColorId)?.Name ?? string.Empty,
            Plate: vehicle.LicensePlate.Value,
            CreatedAt: vehicle.CreatedAt);
    }

    private static Mock<ICustomerRepository> CreateCustomerRepositoryMock(List<Customer>? initialCustomers = null)
    {
        var customers = initialCustomers ?? [];
        var repositoryMock = new Mock<ICustomerRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerId id, CancellationToken _) => customers.FirstOrDefault(c => c.Id == id));

        repositoryMock
            .Setup(x => x.ExistsByTaxDocumentAsync(It.IsAny<TaxDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxDocument taxDocument, CancellationToken _) =>
                customers.Any(c => c.TaxDocument.Value == taxDocument.Value));

        return repositoryMock;
    }

    private static Mock<IVehicleModelRepository> CreateVehicleModelRepositoryMock(List<VehicleModel>? initialVehicleModels = null)
    {
        var vehicleModels = initialVehicleModels ?? [];
        var repositoryMock = new Mock<IVehicleModelRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<VehicleModelId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleModelId id, CancellationToken _) => vehicleModels.FirstOrDefault(v => v.Id == id));

        return repositoryMock;
    }

    private static Mock<IVehicleColorRepository> CreateVehicleColorRepositoryMock(List<VehicleColor>? initialVehicleColors = null)
    {
        var vehicleColors = initialVehicleColors ?? [];
        var repositoryMock = new Mock<IVehicleColorRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<VehicleColorId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleColorId id, CancellationToken _) => vehicleColors.FirstOrDefault(v => v.Id == id));

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
