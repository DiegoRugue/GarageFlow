using GarageFlow.Application.WorkOrders.AddEstimateInventoryItem;
using GarageFlow.Application.WorkOrders.AddEstimateService;
using GarageFlow.Application.WorkOrders.CompleteWorkOrder;
using GarageFlow.Application.WorkOrders.CreateWorkOrder;
using GarageFlow.Application.WorkOrders.SubmitEstimate;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Domain.InventoryItems.Repositories;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Services.Entities;
using GarageFlow.Domain.Services.Repositories;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Repositories;
using GarageFlow.Domain.Users.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.Tests.Shared.Customers;
using GarageFlow.Tests.Shared.InventoryItems;
using GarageFlow.Tests.Shared.Services;
using GarageFlow.Tests.Shared.Vehicles;
using GarageFlow.Tests.Shared.WorkOrders;
using Mediator;
using Moq;
using MediatorUnit = Mediator.Unit;

namespace GarageFlow.Tests.Unit.WorkOrders;

public class WorkOrderHandlersTests
{
    [Fact]
    public async Task CreateWorkOrder_ShouldCreateCreatedWorkOrder_WhenVehicleBelongsToCustomer()
    {
        var customer = new CustomerBuilder().Build();
        var vehicle = new VehicleBuilder().WithCustomerId(customer.Id.Value).Build();
        var workOrders = new List<WorkOrder>();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(workOrders);
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicle]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateWorkOrderHandler(
            workOrderRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleRepositoryMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(new CreateWorkOrderCommand(customer.Id.Value, vehicle.Id.Value), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(customer.Id.Value, result.CustomerId);
        Assert.Equal(vehicle.Id.Value, result.VehicleId);
        Assert.Equal("Created", result.Status);
        Assert.Single(workOrders);
        Assert.Equal(WorkOrderStatus.Created, workOrders[0].Status);

        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateWorkOrder_ShouldThrowBusinessRuleViolationException_WhenVehicleDoesNotBelongToCustomer()
    {
        var customer = new CustomerBuilder().Build();
        var differentCustomer = new CustomerBuilder().WithEmail("another@example.com").Build();
        var vehicle = new VehicleBuilder().WithCustomerId(differentCustomer.Id.Value).Build();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock();
        var customerRepositoryMock = CreateCustomerRepositoryMock([customer]);
        var vehicleRepositoryMock = CreateVehicleRepositoryMock([vehicle]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateWorkOrderHandler(
            workOrderRepositoryMock.Object,
            customerRepositoryMock.Object,
            vehicleRepositoryMock.Object,
            unitOfWorkMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new CreateWorkOrderCommand(customer.Id.Value, vehicle.Id.Value), CancellationToken.None));

        Assert.Equal("Vehicle does not belong to the customer.", exception.Message);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddEstimateInventoryItem_ShouldDecreaseStock_WhenStockIsSufficient()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        var inventoryItem = new InventoryItemBuilder().WithDescription("Oil filter cartridge").WithCost(20m).WithPrice(35m).WithStockQuantity(10).Build();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock([inventoryItem]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new AddEstimateInventoryItemHandler(
            workOrderRepositoryMock.Object,
            inventoryItemRepositoryMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(
            new AddEstimateInventoryItemCommand(
                workOrder.Id.Value,
                estimate.Id.Value,
                inventoryItem.Id.Value,
                Quantity: 2),
            CancellationToken.None);

        Assert.Equal(8, inventoryItem.StockQuantity.Value);
        Assert.Equal(estimate.Id.Value, result.EstimateId);
        Assert.Equal(inventoryItem.Id.Value, result.InventoryItemId);
        Assert.Equal("Oil filter cartridge", result.Description);
        Assert.Equal(2, result.Quantity);
        Assert.Equal(20m, result.UnitCost);
        Assert.Equal(35m, result.UnitPrice);
        Assert.Equal(70m, result.TotalPrice);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddEstimateInventoryItem_ShouldThrowBusinessRuleViolationException_WhenStockIsInsufficient()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        var inventoryItem = new InventoryItemBuilder().WithStockQuantity(1).Build();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock([inventoryItem]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new AddEstimateInventoryItemHandler(
            workOrderRepositoryMock.Object,
            inventoryItemRepositoryMock.Object,
            unitOfWorkMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new AddEstimateInventoryItemCommand(
                    workOrder.Id.Value,
                    estimate.Id.Value,
                    inventoryItem.Id.Value,
                    Quantity: 2),
                CancellationToken.None));

        Assert.Equal("Inventory item stock is insufficient.", exception.Message);
        Assert.Equal(1, inventoryItem.StockQuantity.Value);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddEstimateInventoryItem_ShouldThrowValidationException_WhenQuantityIsInvalid()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        var inventoryItem = new InventoryItemBuilder().Build();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock([inventoryItem]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new AddEstimateInventoryItemHandler(
            workOrderRepositoryMock.Object,
            inventoryItemRepositoryMock.Object,
            unitOfWorkMock.Object);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new AddEstimateInventoryItemCommand(
                    workOrder.Id.Value,
                    estimate.Id.Value,
                    inventoryItem.Id.Value,
                    Quantity: 0),
                CancellationToken.None));

        Assert.Equal("Estimate item quantity must be greater than zero.", exception.Message);
        workOrderRepositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()), Times.Never);
        inventoryItemRepositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddEstimateService_ShouldAddService_WhenServiceExists()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        var service = new ServiceBuilder().WithDescription("Front suspension alignment").WithPrice(180m).Build();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var serviceRepositoryMock = CreateServiceRepositoryMock([service]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new AddEstimateServiceHandler(
            workOrderRepositoryMock.Object,
            serviceRepositoryMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(
            new AddEstimateServiceCommand(workOrder.Id.Value, estimate.Id.Value, service.Id.Value),
            CancellationToken.None);

        Assert.Equal(estimate.Id.Value, result.EstimateId);
        Assert.Equal(service.Id.Value, result.ServiceId);
        Assert.Equal("Front suspension alignment", result.Description);
        Assert.Equal(180m, result.UnitPrice);
        Assert.Equal(180m, result.TotalPrice);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitEstimate_ShouldSubmitDraftEstimate_WhenEstimateHasLines()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new SubmitEstimateHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new SubmitEstimateCommand(workOrder.Id.Value, estimate.Id.Value), CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.WaitingApproval, workOrder.Status);
        Assert.Equal(EstimateStatus.Pending, workOrder.Estimates.Single().Status);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteWorkOrder_ShouldThrowBusinessRuleViolationException_WhenNoEstimateIsApproved()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CompleteWorkOrderHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new CompleteWorkOrderCommand(workOrder.Id.Value), CancellationToken.None));

        Assert.Equal("Work order requires exactly one approved estimate before completion.", exception.Message);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IWorkOrderRepository> CreateWorkOrderRepositoryMock(List<WorkOrder>? initialWorkOrders = null)
    {
        var workOrders = initialWorkOrders ?? [];
        var repositoryMock = new Mock<IWorkOrderRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrderId id, CancellationToken _) => workOrders.SingleOrDefault(workOrder => workOrder.Id == id));

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<WorkOrder>(), It.IsAny<CancellationToken>()))
            .Returns((WorkOrder workOrder, CancellationToken _) =>
            {
                workOrders.Add(workOrder);
                return Task.CompletedTask;
            });

        return repositoryMock;
    }

    private static Mock<ICustomerRepository> CreateCustomerRepositoryMock(List<Customer>? initialCustomers = null)
    {
        var customers = initialCustomers ?? [];
        var repositoryMock = new Mock<ICustomerRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerId id, CancellationToken _) => customers.SingleOrDefault(customer => customer.Id == id));

        repositoryMock
            .Setup(x => x.ExistsByTaxDocumentAsync(It.IsAny<TaxDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxDocument taxDocument, CancellationToken _) =>
                customers.Any(customer => customer.TaxDocument.Value == taxDocument.Value));

        return repositoryMock;
    }

    private static Mock<IVehicleRepository> CreateVehicleRepositoryMock(List<Vehicle>? initialVehicles = null)
    {
        var vehicles = initialVehicles ?? [];
        var repositoryMock = new Mock<IVehicleRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<VehicleId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleId id, CancellationToken _) => vehicles.SingleOrDefault(vehicle => vehicle.Id == id));

        return repositoryMock;
    }

    private static Mock<IInventoryItemRepository> CreateInventoryItemRepositoryMock(List<InventoryItem>? initialInventoryItems = null)
    {
        var inventoryItems = initialInventoryItems ?? [];
        var repositoryMock = new Mock<IInventoryItemRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItemId id, CancellationToken _) => inventoryItems.SingleOrDefault(item => item.Id == id));

        return repositoryMock;
    }

    private static Mock<IServiceRepository> CreateServiceRepositoryMock(List<Service>? initialServices = null)
    {
        var services = initialServices ?? [];
        var repositoryMock = new Mock<IServiceRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<ServiceId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceId id, CancellationToken _) => services.SingleOrDefault(service => service.Id == id));

        return repositoryMock;
    }

    private static Mock<IUserRepository> CreateUserRepositoryMock(List<User>? initialUsers = null)
    {
        var users = initialUsers ?? [];
        var repositoryMock = new Mock<IUserRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserId id, CancellationToken _) => users.SingleOrDefault(user => user.Id == id));

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
