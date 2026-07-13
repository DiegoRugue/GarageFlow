using System.Text.Json;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Application.Services.Ports;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrderIntake;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.Services.Entities;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Moq;

namespace GarageFlow.Tests.Unit.WorkOrders;

public sealed class CreateWorkOrderIntakeHandlerTests
{
    [Fact]
    public async Task Handle_CreatesCompleteGraph_WithOneServiceAndNoInventory()
    {
        var fixture = new Fixture();

        var result = await fixture.HandleAsync(fixture.ValidCommand());

        Assert.False(result.IsReplay);
        Assert.Single(result.ServiceIds);
        Assert.Empty(result.InventoryItemIds);
        Assert.Equal("Received", result.Status);
        Assert.Equal(result.WorkOrderId, fixture.AddedWorkOrder!.Id.Value);
        Assert.Single(fixture.AddedWorkOrder.Estimates);
        Assert.Single(fixture.AddedWorkOrder.Estimates.Single().ServiceLines);
        fixture.RequestStore.Verify(store => store.CompleteAsync(
            fixture.RequestId,
            result.WorkOrderId,
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CreatesMultipleServicesAndInventory_AndReservesStock()
    {
        var fixture = new Fixture();
        var command = fixture.ValidCommand(
            services:
            [
                new("Oil change", 120m),
                new("Alignment", 85.50m)
            ],
            inventoryItems:
            [
                new("Oil filter", "Premium filter", "part", 20m, 40m, 10, 2),
                new("Engine oil", "Synthetic oil", "SUPPLY", 25m, 55m, 8, 3)
            ]);

        var result = await fixture.HandleAsync(command);

        Assert.Equal(2, result.ServiceIds.Count);
        Assert.Equal(2, result.InventoryItemIds.Count);
        Assert.Equal([8, 5], fixture.AddedInventoryItems.Select(item => item.StockQuantity.Value));
        var estimate = Assert.Single(fixture.AddedWorkOrder!.Estimates);
        Assert.Equal(2, estimate.ServiceLines.Count);
        Assert.Equal(2, estimate.InventoryLines.Count);
        Assert.Equal([2, 3], estimate.InventoryLines.Select(line => line.Quantity.Value));
    }

    [Fact]
    public async Task Handle_RejectsZeroServicesBeforeClaim()
    {
        var fixture = new Fixture();

        await Assert.ThrowsAsync<ValidationException>(() => fixture.HandleAsync(fixture.ValidCommand(services: [])));

        fixture.RequestStore.Verify(store => store.ClaimAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsDuplicateNormalizedServiceDescriptionsBeforeClaim()
    {
        var fixture = new Fixture();
        var command = fixture.ValidCommand(services: [new("Oil change", 10m), new(" oil CHANGE ", 20m)]);

        await Assert.ThrowsAsync<ValidationException>(() => fixture.HandleAsync(command));

        fixture.RequestStore.Verify(store => store.ClaimAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsDuplicateNormalizedInventoryNamesBeforeClaim()
    {
        var fixture = new Fixture();
        var command = fixture.ValidCommand(inventoryItems:
        [
            new("Oil filter", "One", "Part", 1m, 2m, 5, 1),
            new(" oil FILTER ", "Two", "Part", 1m, 2m, 5, 1)
        ]);

        await Assert.ThrowsAsync<ValidationException>(() => fixture.HandleAsync(command));

        fixture.RequestStore.Verify(store => store.ClaimAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConstructsAllValueObjectsBeforeCheckingDuplicates()
    {
        var fixture = new Fixture();
        var command = fixture.ValidCommand(services: [new("Oil change", 10m), new(" oil CHANGE ", 20m)]) with
        {
            Customer = new("invalid-tax-document", "Ana Silva", "ana@example.com", "11912345678")
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() => fixture.HandleAsync(command));

        Assert.Equal("Tax document must be a valid CPF (11 digits) or CNPJ (14 digits).", exception.Message);
        fixture.RequestStore.Verify(store => store.ClaimAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsExistingTaxDocumentWithoutCreatingGraph()
    {
        var fixture = new Fixture(existingCustomer: true);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => fixture.HandleAsync(fixture.ValidCommand()));

        fixture.CustomerRepository.Verify(repository => repository.AddAsync(
            It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.RequestStore.Verify(store => store.CompleteAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsExistingPlateWithoutCreatingGraph()
    {
        var fixture = new Fixture(existingVehicle: true);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => fixture.HandleAsync(fixture.ValidCommand()));

        fixture.VehicleRepository.Verify(repository => repository.AddAsync(
            It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.RequestStore.Verify(store => store.CompleteAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsInsufficientStockWithoutCompletingReceipt()
    {
        var fixture = new Fixture();
        var command = fixture.ValidCommand(inventoryItems:
        [
            new("Oil filter", "Premium filter", "Part", 20m, 40m, 1, 2)
        ]);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => fixture.HandleAsync(command));

        fixture.RequestStore.Verify(store => store.CompleteAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReusesReferencesByCaseInsensitiveName()
    {
        var fixture = new Fixture(reuseReferences: true);

        var result = await fixture.HandleAsync(fixture.ValidCommand(brand: " hONDa ", model: " cIVic ", color: " bLACK "));

        Assert.Equal(fixture.ExistingBrand!.Id, fixture.AddedVehicle!.VehicleBrandId);
        Assert.Equal(fixture.ExistingModel!.Id, fixture.AddedVehicle.VehicleModelId);
        Assert.Equal(fixture.ExistingColor!.Id, fixture.AddedVehicle.VehicleColorId);
        fixture.BrandRepository.Verify(repository => repository.AddAsync(
            It.IsAny<VehicleBrand>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.ModelRepository.Verify(repository => repository.AddAsync(
            It.IsAny<VehicleModel>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.ColorRepository.Verify(repository => repository.AddAsync(
            It.IsAny<VehicleColor>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(result.IsReplay);
    }

    [Fact]
    public async Task Handle_WhenRepositoryFails_DoesNotCompleteReceipt()
    {
        var fixture = new Fixture(failCustomerAdd: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.HandleAsync(fixture.ValidCommand()));

        fixture.RequestStore.Verify(store => store.CompleteAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AcquiredClaim_CreatesANewGraph()
    {
        var fixture = new Fixture();

        await fixture.HandleAsync(fixture.ValidCommand());

        fixture.WorkOrderRepository.Verify(repository => repository.AddAsync(
            It.IsAny<WorkOrder>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(0, fixture.EstimateCountWhenWorkOrderWasAdded);
    }

    [Fact]
    public async Task Handle_CompletedEqualClaim_ReturnsStoredResponseAsReplay()
    {
        var stored = new StoredWorkOrderIntakeResponse(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            [Guid.NewGuid()], [], "Received", DateTime.UtcNow);
        var fixture = new Fixture(storedResponse: stored);

        var result = await fixture.HandleAsync(fixture.ValidCommand());

        Assert.True(result.IsReplay);
        Assert.Equal(stored.WorkOrderId, result.WorkOrderId);
        fixture.WorkOrderRepository.Verify(repository => repository.AddAsync(
            It.IsAny<WorkOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CompletedDifferentHash_ThrowsConflict()
    {
        var stored = new StoredWorkOrderIntakeResponse(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [], [], "Received", DateTime.UtcNow);
        var fixture = new Fixture(storedResponse: stored, storedHash: new string('0', 64));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => fixture.HandleAsync(fixture.ValidCommand()));

        fixture.WorkOrderRepository.Verify(repository => repository.AddAsync(
            It.IsAny<WorkOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture
    {
        public Guid RequestId { get; } = Guid.NewGuid();
        public Mock<ICustomerRepository> CustomerRepository { get; } = new(MockBehavior.Strict);
        public Mock<IVehicleRepository> VehicleRepository { get; } = new(MockBehavior.Strict);
        public Mock<IVehicleBrandRepository> BrandRepository { get; } = new(MockBehavior.Strict);
        public Mock<IVehicleModelRepository> ModelRepository { get; } = new(MockBehavior.Strict);
        public Mock<IVehicleColorRepository> ColorRepository { get; } = new(MockBehavior.Strict);
        public Mock<IServiceRepository> ServiceRepository { get; } = new(MockBehavior.Strict);
        public Mock<IInventoryItemRepository> InventoryRepository { get; } = new(MockBehavior.Strict);
        public Mock<IWorkOrderRepository> WorkOrderRepository { get; } = new(MockBehavior.Strict);
        public Mock<IWorkOrderIntakeRequestStore> RequestStore { get; } = new(MockBehavior.Strict);
        public VehicleBrand? ExistingBrand { get; }
        public VehicleModel? ExistingModel { get; }
        public VehicleColor? ExistingColor { get; }
        public Vehicle? AddedVehicle { get; private set; }
        public WorkOrder? AddedWorkOrder { get; private set; }
        public int? EstimateCountWhenWorkOrderWasAdded { get; private set; }
        public List<InventoryItem> AddedInventoryItems { get; } = [];

        private readonly CreateWorkOrderIntakeHandler _handler;

        public Fixture(
            bool existingCustomer = false,
            bool existingVehicle = false,
            bool reuseReferences = false,
            bool failCustomerAdd = false,
            StoredWorkOrderIntakeResponse? storedResponse = null,
            string? storedHash = null)
        {
            if (reuseReferences)
            {
                ExistingBrand = VehicleBrand.Create("Honda");
                ExistingModel = VehicleModel.Create(ExistingBrand.Id, "Civic");
                ExistingColor = VehicleColor.Create("Black");
            }

            RequestStore.Setup(store => store.ClaimAsync(RequestId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<Guid, string, CancellationToken>((_, hash, _) => Task.FromResult(
                    storedResponse is null
                        ? new IntakeRequestClaim(IntakeRequestClaimState.Acquired, hash, null)
                        : new IntakeRequestClaim(
                            IntakeRequestClaimState.Completed,
                            storedHash ?? hash,
                            JsonSerializer.Serialize(storedResponse))));
            CustomerRepository.Setup(repository => repository.ExistsByTaxDocumentAsync(
                    It.IsAny<GarageFlow.SharedKernel.Domain.ValueObjects.TaxDocument>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCustomer);
            VehicleRepository.Setup(repository => repository.ExistsByLicensePlateAsync(
                    It.IsAny<GarageFlow.Domain.Vehicles.ValueObjects.LicensePlate>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingVehicle);
            BrandRepository.Setup(repository => repository.GetByNameAsync(
                    It.IsAny<GarageFlow.Domain.Vehicles.ValueObjects.VehicleBrandName>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ExistingBrand);
            ModelRepository.Setup(repository => repository.GetByNameAsync(
                    It.IsAny<GarageFlow.Domain.Vehicles.ValueObjects.VehicleBrandId>(),
                    It.IsAny<GarageFlow.Domain.Vehicles.ValueObjects.VehicleModelName>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ExistingModel);
            ColorRepository.Setup(repository => repository.GetByNameAsync(
                    It.IsAny<GarageFlow.Domain.Vehicles.ValueObjects.VehicleColorName>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ExistingColor);
            BrandRepository.Setup(repository => repository.AddAsync(It.IsAny<VehicleBrand>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            ModelRepository.Setup(repository => repository.AddAsync(It.IsAny<VehicleModel>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            ColorRepository.Setup(repository => repository.AddAsync(It.IsAny<VehicleColor>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            CustomerRepository.Setup(repository => repository.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
                .Returns(failCustomerAdd ? Task.FromException(new InvalidOperationException("failure")) : Task.CompletedTask);
            VehicleRepository.Setup(repository => repository.AddAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()))
                .Callback<Vehicle, CancellationToken>((vehicle, _) => AddedVehicle = vehicle)
                .Returns(Task.CompletedTask);
            ServiceRepository.Setup(repository => repository.AddAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            InventoryRepository.Setup(repository => repository.AddAsync(It.IsAny<InventoryItem>(), It.IsAny<CancellationToken>()))
                .Callback<InventoryItem, CancellationToken>((item, _) => AddedInventoryItems.Add(item))
                .Returns(Task.CompletedTask);
            WorkOrderRepository.Setup(repository => repository.AddAsync(It.IsAny<WorkOrder>(), It.IsAny<CancellationToken>()))
                .Callback<WorkOrder, CancellationToken>((workOrder, _) =>
                {
                    AddedWorkOrder = workOrder;
                    EstimateCountWhenWorkOrderWasAdded = workOrder.Estimates.Count;
                })
                .Returns(Task.CompletedTask);
            RequestStore.Setup(store => store.CompleteAsync(
                    RequestId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _handler = new CreateWorkOrderIntakeHandler(
                RequestStore.Object,
                CustomerRepository.Object,
                VehicleRepository.Object,
                new ResolveVehicleReferencesHandler(
                    BrandRepository.Object,
                    ModelRepository.Object,
                    ColorRepository.Object),
                ServiceRepository.Object,
                InventoryRepository.Object,
                WorkOrderRepository.Object);
        }

        public CreateWorkOrderIntakeCommand ValidCommand(
            IReadOnlyList<IntakeServiceInput>? services = null,
            IReadOnlyList<IntakeInventoryItemInput>? inventoryItems = null,
            string brand = "Honda",
            string model = "Civic",
            string color = "Black") => new(
                RequestId,
                new("529.982.247-25", "Ana Silva", "ana@example.com", "+55 (11) 91234-5678"),
                new("ABC1D23", 2024, brand, model, color),
                services ?? [new("Oil change", 120m)],
                inventoryItems ?? []);

        public async Task<CreateWorkOrderIntakeResult> HandleAsync(CreateWorkOrderIntakeCommand command) =>
            await _handler.Handle(command, CancellationToken.None);
    }
}
