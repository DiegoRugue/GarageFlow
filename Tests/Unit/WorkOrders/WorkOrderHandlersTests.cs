using GarageFlow.Application.WorkOrders;
using GarageFlow.Application.WorkOrders.Abstractions;
using GarageFlow.Application.WorkOrders.AddEstimateInventoryItem;
using GarageFlow.Application.WorkOrders.AddEstimateService;
using GarageFlow.Application.WorkOrders.ApproveMyEstimate;
using GarageFlow.Application.WorkOrders.CancelWorkOrder;
using GarageFlow.Application.WorkOrders.CompleteEstimateService;
using GarageFlow.Application.WorkOrders.CreateEstimate;
using GarageFlow.Application.WorkOrders.CreateWorkOrder;
using GarageFlow.Application.WorkOrders.DeliverWorkOrder;
using GarageFlow.Application.WorkOrders.GetAverageServiceTime;
using GarageFlow.Application.WorkOrders.GetMyWorkOrderById;
using GarageFlow.Application.WorkOrders.GetWorkOrderById;
using GarageFlow.Application.WorkOrders.ListMyWorkOrders;
using GarageFlow.Application.WorkOrders.ListWorkOrders;
using GarageFlow.Application.WorkOrders.RejectMyEstimate;
using GarageFlow.Application.WorkOrders.StartDiagnosis;
using GarageFlow.Application.WorkOrders.StartEstimateService;
using GarageFlow.Application.WorkOrders.StartWork;
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
using GarageFlow.Domain.Users.Enums;
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
using GarageFlow.Tests.Shared.Users;
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
    public async Task AddEstimateInventoryItem_ShouldBeginTransactionBeforeLoadingInventoryForReservation()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        var inventoryItem = new InventoryItemBuilder().WithStockQuantity(10).Build();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock([inventoryItem]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var sequence = new MockSequence();

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        inventoryItemRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AddEstimateInventoryItemHandler(
            workOrderRepositoryMock.Object,
            inventoryItemRepositoryMock.Object,
            unitOfWorkMock.Object);

        await handler.Handle(
            new AddEstimateInventoryItemCommand(
                workOrder.Id.Value,
                estimate.Id.Value,
                inventoryItem.Id.Value,
                Quantity: 1),
            CancellationToken.None);

        inventoryItemRepositoryMock.Verify(
            x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        inventoryItemRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
    public async Task AddEstimateInventoryItem_ShouldNotDecreaseStock_WhenWorkOrderCannotBeChanged()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimateId = workOrder.Estimates.Single().Id;
        var inventoryItem = new InventoryItemBuilder().WithStockQuantity(6).Build();
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
                    estimateId.Value,
                    inventoryItem.Id.Value,
                    Quantity: 1),
                CancellationToken.None));

        Assert.Equal("Approved or in-progress work orders cannot be changed.", exception.Message);
        Assert.Equal(6, inventoryItem.StockQuantity.Value);
        inventoryItemRepositoryMock.Verify(
            x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
    public async Task AddEstimateService_ShouldBeginTransactionBeforeLoadingWorkOrderForEstimateApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        var service = new ServiceBuilder().WithPrice(120m).Build();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var serviceRepositoryMock = CreateServiceRepositoryMock([service]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var sequence = new MockSequence();

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        serviceRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdAsync(It.IsAny<ServiceId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AddEstimateServiceHandler(
            workOrderRepositoryMock.Object,
            serviceRepositoryMock.Object,
            unitOfWorkMock.Object);

        await handler.Handle(
            new AddEstimateServiceCommand(workOrder.Id.Value, estimate.Id.Value, service.Id.Value),
            CancellationToken.None);

        workOrderRepositoryMock.Verify(
            x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddEstimateService_ShouldNotLoadService_WhenEstimateCannotBeEdited()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimateId = workOrder.Estimates.Single().Id;
        var service = new ServiceBuilder().WithPrice(120m).Build();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var serviceRepositoryMock = CreateServiceRepositoryMock([service]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new AddEstimateServiceHandler(
            workOrderRepositoryMock.Object,
            serviceRepositoryMock.Object,
            unitOfWorkMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new AddEstimateServiceCommand(workOrder.Id.Value, estimateId.Value, service.Id.Value),
                CancellationToken.None));

        Assert.Equal("Approved or in-progress work orders cannot be changed.", exception.Message);
        serviceRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<ServiceId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateEstimate_ShouldCreateDraftEstimate_WhenWorkOrderExists()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CreateEstimateHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new CreateEstimateCommand(workOrder.Id.Value), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(workOrder.Id.Value, result.WorkOrderId);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(0m, result.TotalAmount);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateEstimate_ShouldBeginTransactionBeforeLoadingWorkOrderForEstimateApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var sequence = new MockSequence();

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateEstimateHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        await handler.Handle(new CreateEstimateCommand(workOrder.Id.Value), CancellationToken.None);

        workOrderRepositoryMock.Verify(
            x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitEstimate_ShouldSubmitDraftEstimate_WhenEstimateHasLines()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var emailSenderMock = new Mock<ICustomerApprovalEmailSender>();
        var handler = new SubmitEstimateHandler(
            workOrderRepositoryMock.Object,
            unitOfWorkMock.Object,
            emailSenderMock.Object);

        var result = await handler.Handle(new SubmitEstimateCommand(workOrder.Id.Value, estimate.Id.Value), CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.WaitingApproval, workOrder.Status);
        Assert.Equal(EstimateStatus.Pending, workOrder.Estimates.Single().Status);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitEstimate_ShouldBeginTransactionBeforeLoadingWorkOrderForEstimateMutation()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var sequence = new MockSequence();
        var emailSenderMock = new Mock<ICustomerApprovalEmailSender>();

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SubmitEstimateHandler(
            workOrderRepositoryMock.Object,
            unitOfWorkMock.Object,
            emailSenderMock.Object);

        await handler.Handle(new SubmitEstimateCommand(workOrder.Id.Value, estimate.Id.Value), CancellationToken.None);

        workOrderRepositoryMock.Verify(
            x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartEstimateService_ShouldStartServiceLine_AndCommit()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimate = workOrder.Estimates.Single();
        var serviceLine = estimate.ServiceLines.Single();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new StartEstimateServiceHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        await handler.Handle(
            new StartEstimateServiceCommand(workOrder.Id.Value, estimate.Id.Value, serviceLine.Id.Value),
            CancellationToken.None);

        Assert.Equal(EstimateServiceLineStatus.InProgress, serviceLine.Status);
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteEstimateService_ShouldCompleteServiceLine_AndCommit()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimate = workOrder.Estimates.Single();
        var serviceLine = estimate.ServiceLines.Single();
        workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CompleteEstimateServiceHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        await handler.Handle(
            new CompleteEstimateServiceCommand(workOrder.Id.Value, estimate.Id.Value, serviceLine.Id.Value),
            CancellationToken.None);

        Assert.Equal(EstimateServiceLineStatus.Completed, serviceLine.Status);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartEstimateService_ShouldRollback_WhenWorkOrderIsMissing()
    {
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new StartEstimateServiceHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);
        var workOrderId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new StartEstimateServiceCommand(workOrderId, Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None));

        Assert.Equal($"Work order with ID '{workOrderId}' was not found.", exception.Message);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteEstimateService_ShouldRollback_WhenWorkOrderIsMissing()
    {
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CompleteEstimateServiceHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);
        var workOrderId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new CompleteEstimateServiceCommand(workOrderId, Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None));

        Assert.Equal($"Work order with ID '{workOrderId}' was not found.", exception.Message);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitEstimate_ShouldSendApprovalEmail_WhenWorkOrderMovesToWaitingApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var emailSenderMock = new Mock<ICustomerApprovalEmailSender>();
        emailSenderMock
            .Setup(x => x.SendEstimateWaitingApprovalAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new SubmitEstimateHandler(
            workOrderRepositoryMock.Object,
            unitOfWorkMock.Object,
            emailSenderMock.Object);

        await handler.Handle(new SubmitEstimateCommand(workOrder.Id.Value, estimate.Id.Value), CancellationToken.None);

        emailSenderMock.Verify(
            x => x.SendEstimateWaitingApprovalAsync(
                workOrder.Id.Value,
                estimate.Id.Value,
                workOrder.CustomerId.Value,
                It.IsAny<CancellationToken>()),
            Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitEstimate_ShouldCommitBeforeSendingApprovalEmail_WhenWorkOrderMovesToWaitingApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var emailSenderMock = new Mock<ICustomerApprovalEmailSender>();
        var hasCommittedTransaction = false;

        unitOfWorkMock
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock
            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => hasCommittedTransaction = true)
            .Returns(Task.CompletedTask);

        emailSenderMock
            .Setup(x => x.SendEstimateWaitingApprovalAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(hasCommittedTransaction))
            .Returns(Task.CompletedTask);

        var handler = new SubmitEstimateHandler(
            workOrderRepositoryMock.Object,
            unitOfWorkMock.Object,
            emailSenderMock.Object);

        await handler.Handle(new SubmitEstimateCommand(workOrder.Id.Value, estimate.Id.Value), CancellationToken.None);

        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        emailSenderMock.Verify(
            x => x.SendEstimateWaitingApprovalAsync(
                workOrder.Id.Value,
                estimate.Id.Value,
                workOrder.CustomerId.Value,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitEstimate_ShouldNotSendApprovalEmail_WhenCommitFails()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var emailSenderMock = new Mock<ICustomerApprovalEmailSender>();

        unitOfWorkMock
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Commit failed"));

        var handler = new SubmitEstimateHandler(
            workOrderRepositoryMock.Object,
            unitOfWorkMock.Object,
            emailSenderMock.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(
                new SubmitEstimateCommand(workOrder.Id.Value, estimate.Id.Value),
                CancellationToken.None));

        Assert.Equal("Commit failed", exception.Message);
        emailSenderMock.Verify(
            x => x.SendEstimateWaitingApprovalAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitEstimate_ShouldNotRollback_WhenApprovalEmailSendFailsAfterCommit()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var emailSenderMock = new Mock<ICustomerApprovalEmailSender>();

        emailSenderMock
            .Setup(x => x.SendEstimateWaitingApprovalAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Email send failed"));

        var handler = new SubmitEstimateHandler(
            workOrderRepositoryMock.Object,
            unitOfWorkMock.Object,
            emailSenderMock.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.Handle(
                new SubmitEstimateCommand(workOrder.Id.Value, estimate.Id.Value),
                CancellationToken.None));

        Assert.Equal("Email send failed", exception.Message);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitEstimate_ShouldNotSendApprovalEmail_WhenWorkOrderWasAlreadyWaitingApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var firstEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, firstEstimate.Id);
        workOrder.SubmitEstimate(firstEstimate.Id);
        var secondEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, secondEstimate.Id);

        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var emailSenderMock = new Mock<ICustomerApprovalEmailSender>();
        var handler = new SubmitEstimateHandler(
            workOrderRepositoryMock.Object,
            unitOfWorkMock.Object,
            emailSenderMock.Object);

        await handler.Handle(new SubmitEstimateCommand(workOrder.Id.Value, secondEstimate.Id.Value), CancellationToken.None);

        Assert.Equal(WorkOrderStatus.WaitingApproval, workOrder.Status);
        emailSenderMock.Verify(
            x => x.SendEstimateWaitingApprovalAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartDiagnosis_ShouldTransitionToDiagnosing_WhenWorkOrderIsCreated()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new StartDiagnosisHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new StartDiagnosisCommand(workOrder.Id.Value), CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.Diagnosing, workOrder.Status);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartWork_ShouldTransitionToInProgress_WhenWorkOrderIsApproved()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new StartWorkHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new StartWorkCommand(workOrder.Id.Value), CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelWorkOrder_ShouldTransitionToCancelled_WhenWorkOrderIsCreated()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CancelWorkOrderHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new CancelWorkOrderCommand(workOrder.Id.Value), CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.Cancelled, workOrder.Status);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeliverWorkOrder_ShouldTransitionToDelivered_WhenWorkOrderIsCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildCompleted();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeliverWorkOrderHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        var result = await handler.Handle(new DeliverWorkOrderCommand(workOrder.Id.Value), CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.Delivered, workOrder.Status);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeliverWorkOrder_ShouldRollback_WhenWorkOrderIsNotCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeliverWorkOrderHandler(workOrderRepositoryMock.Object, unitOfWorkMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new DeliverWorkOrderCommand(workOrder.Id.Value), CancellationToken.None));

        Assert.Equal("Only completed work orders can be delivered.", exception.Message);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveMyEstimate_ShouldApproveEstimate_WhenWorkOrderBelongsToCustomer()
    {
        var customerId = CustomerId.New();
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(customerId)
            .WithEmail("customer.approve@example.com")
            .Build();

        var workOrder = WorkOrder.Create(customerId, VehicleId.New());
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.SubmitEstimate(estimate.Id);

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ApproveMyEstimateHandler(
            userRepositoryMock.Object,
            workOrderRepositoryMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(
            new ApproveMyEstimateCommand(user.Id.Value, workOrder.Id.Value, estimate.Id.Value),
            CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.Approved, workOrder.Status);
        Assert.Equal(EstimateStatus.Approved, workOrder.Estimates.Single().Status);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveMyEstimate_ShouldBeginTransactionBeforeLoadingWorkOrderForEstimateApproval()
    {
        var customerId = CustomerId.New();
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(customerId)
            .WithEmail("customer.approve.sequence@example.com")
            .Build();

        var workOrder = WorkOrder.Create(customerId, VehicleId.New());
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.SubmitEstimate(estimate.Id);

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var sequence = new MockSequence();

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ApproveMyEstimateHandler(
            userRepositoryMock.Object,
            workOrderRepositoryMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(
            new ApproveMyEstimateCommand(user.Id.Value, workOrder.Id.Value, estimate.Id.Value),
            CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApproveMyEstimate_ShouldThrowNotFoundException_WhenWorkOrderBelongsToAnotherCustomer()
    {
        var ownerCustomerId = CustomerId.New();
        var requesterCustomerId = CustomerId.New();
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(requesterCustomerId)
            .WithEmail("customer.other-owner@example.com")
            .Build();

        var workOrder = WorkOrder.Create(ownerCustomerId, VehicleId.New());
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.SubmitEstimate(estimate.Id);

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ApproveMyEstimateHandler(
            userRepositoryMock.Object,
            workOrderRepositoryMock.Object,
            unitOfWorkMock.Object);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new ApproveMyEstimateCommand(user.Id.Value, workOrder.Id.Value, estimate.Id.Value),
                CancellationToken.None));

        Assert.Equal($"Work order with ID '{workOrder.Id.Value}' was not found.", exception.Message);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectMyEstimate_ShouldRejectEstimate_AndRestoreStock_WhenWorkOrderBelongsToCustomer()
    {
        var customerId = CustomerId.New();
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(customerId)
            .WithEmail("customer.reject@example.com")
            .Build();

        var inventoryItem = new InventoryItemBuilder().WithStockQuantity(8).Build();
        var workOrder = WorkOrder.Create(customerId, VehicleId.New());
        var estimate = workOrder.CreateEstimate();
        workOrder.AddInventoryLine(
            estimate.Id,
            inventoryItem.Id,
            Description.Create("Battery replacement"),
            EstimateItemQuantity.Create(2),
            Price.Create(110m),
            Price.Create(180m));
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.SubmitEstimate(estimate.Id);

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock([inventoryItem]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new RejectMyEstimateHandler(
            userRepositoryMock.Object,
            workOrderRepositoryMock.Object,
            inventoryItemRepositoryMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(
            new RejectMyEstimateCommand(user.Id.Value, workOrder.Id.Value, estimate.Id.Value),
            CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(EstimateStatus.Rejected, workOrder.Estimates.Single().Status);
        Assert.Equal(10, inventoryItem.StockQuantity.Value);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        inventoryItemRepositoryMock.Verify(
            x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        inventoryItemRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectMyEstimate_ShouldBeginTransactionBeforeLoadingWorkOrderForEstimateApproval()
    {
        var customerId = CustomerId.New();
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(customerId)
            .WithEmail("customer.reject.sequence@example.com")
            .Build();

        var inventoryItem = new InventoryItemBuilder().WithStockQuantity(8).Build();
        var workOrder = WorkOrder.Create(customerId, VehicleId.New());
        var estimate = workOrder.CreateEstimate();
        workOrder.AddInventoryLine(
            estimate.Id,
            inventoryItem.Id,
            Description.Create("Battery replacement"),
            EstimateItemQuantity.Create(2),
            Price.Create(110m),
            Price.Create(180m));
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.SubmitEstimate(estimate.Id);

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock([inventoryItem]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var sequence = new MockSequence();

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        inventoryItemRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RejectMyEstimateHandler(
            userRepositoryMock.Object,
            workOrderRepositoryMock.Object,
            inventoryItemRepositoryMock.Object,
            unitOfWorkMock.Object);

        var result = await handler.Handle(
            new RejectMyEstimateCommand(user.Id.Value, workOrder.Id.Value, estimate.Id.Value),
            CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
    }

    [Fact]
    public async Task RejectMyEstimate_ShouldThrowNotFoundException_WhenWorkOrderBelongsToAnotherCustomer()
    {
        var ownerCustomerId = CustomerId.New();
        var requesterCustomerId = CustomerId.New();
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(requesterCustomerId)
            .WithEmail("customer.reject.other-owner@example.com")
            .Build();

        var workOrder = WorkOrder.Create(ownerCustomerId, VehicleId.New());
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.SubmitEstimate(estimate.Id);

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new RejectMyEstimateHandler(
            userRepositoryMock.Object,
            workOrderRepositoryMock.Object,
            inventoryItemRepositoryMock.Object,
            unitOfWorkMock.Object);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new RejectMyEstimateCommand(user.Id.Value, workOrder.Id.Value, estimate.Id.Value),
                CancellationToken.None));

        Assert.Equal($"Work order with ID '{workOrder.Id.Value}' was not found.", exception.Message);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        inventoryItemRepositoryMock.Verify(
            x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyWorkOrderById_ShouldNotExposeInventoryCost()
    {
        var customerId = CustomerId.New();
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(customerId)
            .WithEmail("customer.get-by-id@example.com")
            .Build();

        var workOrderId = Guid.NewGuid();
        var estimateId = Guid.NewGuid();
        var inventoryLineId = Guid.NewGuid();
        var serviceLineId = Guid.NewGuid();
        var details = CreateWorkOrderDetailsReadModel(
            workOrderId,
            customerId.Value,
            estimates:
            [
                new WorkOrderEstimateReadModel(
                    Id: estimateId,
                    WorkOrderId: workOrderId,
                    Status: "Pending",
                    TotalAmount: 450m,
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow,
                    InventoryLines:
                    [
                        new WorkOrderInventoryLineReadModel(
                            Id: inventoryLineId,
                            EstimateId: estimateId,
                            InventoryItemId: Guid.NewGuid(),
                            DescriptionSnapshot: "Starter motor",
                            Quantity: 1,
                            UnitCost: 220m,
                            UnitPrice: 300m,
                            TotalPrice: 300m)
                    ],
                    ServiceLines:
                    [
                        new WorkOrderServiceLineReadModel(
                            Id: serviceLineId,
                            EstimateId: estimateId,
                            ServiceId: Guid.NewGuid(),
                            DescriptionSnapshot: "Diagnostics",
                            UnitPrice: 150m,
                            TotalPrice: 150m,
                            Status: "Pending",
                            StartedAt: null,
                            CompletedAt: null)
                    ])
            ]);

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(customerDetailsById: [details]);
        var handler = new GetMyWorkOrderByIdHandler(
            userRepositoryMock.Object,
            workOrderRepositoryMock.Object);

        var result = await handler.Handle(
            new GetMyWorkOrderByIdQuery(user.Id.Value, workOrderId),
            CancellationToken.None);

        var estimate = Assert.Single(result.Estimates);
        var inventoryLine = Assert.Single(estimate.InventoryLines);

        Assert.Equal(workOrderId, result.Id);
        Assert.Equal("Starter motor", inventoryLine.Description);
        Assert.Equal(1, inventoryLine.Quantity);
        Assert.Equal(300m, inventoryLine.UnitPrice);
        Assert.Equal(300m, inventoryLine.TotalPrice);
        Assert.DoesNotContain(
            typeof(CustomerWorkOrderInventoryLineDto).GetProperties(),
            property => property.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListMyWorkOrders_ShouldReturnMappedPage_WhenUserIsCustomer()
    {
        var customerId = CustomerId.New();
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(customerId)
            .WithEmail("customer.list@example.com")
            .Build();

        var pagedDetails = Enumerable.Range(0, 6)
            .Select(_ => CreateWorkOrderDetailsReadModel(Guid.NewGuid(), customerId.Value))
            .ToList();
        var expectedWorkOrderId = pagedDetails[^1].Id;
        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(customerDetailsList: pagedDetails);
        var handler = new ListMyWorkOrdersHandler(userRepositoryMock.Object, workOrderRepositoryMock.Object);

        var result = await handler.Handle(
            new ListMyWorkOrdersQuery(user.Id.Value, Page: 2, PageSize: 5),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(expectedWorkOrderId, result.Items[0].Id);
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        workOrderRepositoryMock.Verify(
            x => x.ListCustomerDetailsAsync(2, 5, customerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListMyWorkOrders_ShouldThrowUnauthorizedAccessException_WhenUserIsNotCustomer()
    {
        var user = new UserBuilder()
            .WithRole(UserRole.Attendant)
            .WithEmail("attendant.list@example.com")
            .Build();

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock();
        var handler = new ListMyWorkOrdersHandler(userRepositoryMock.Object, workOrderRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await handler.Handle(
                new ListMyWorkOrdersQuery(user.Id.Value),
                CancellationToken.None));

        Assert.Equal("Authenticated user is not a customer user.", exception.Message);
        workOrderRepositoryMock.Verify(
            x => x.ListCustomerDetailsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CustomerId>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RejectMyEstimate_ShouldRollback_WhenInventoryItemIsMissing()
    {
        var customerId = CustomerId.New();
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(customerId)
            .WithEmail("customer.reject.rollback@example.com")
            .Build();

        var missingInventoryItemId = InventoryItemId.New();
        var workOrder = WorkOrder.Create(customerId, VehicleId.New());
        var estimate = workOrder.CreateEstimate();
        workOrder.AddInventoryLine(
            estimate.Id,
            missingInventoryItemId,
            Description.Create("Missing stock item"),
            EstimateItemQuantity.Create(1),
            Price.Create(10m),
            Price.Create(15m));
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.SubmitEstimate(estimate.Id);

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new RejectMyEstimateHandler(
            userRepositoryMock.Object,
            workOrderRepositoryMock.Object,
            inventoryItemRepositoryMock.Object,
            unitOfWorkMock.Object);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new RejectMyEstimateCommand(user.Id.Value, workOrder.Id.Value, estimate.Id.Value),
                CancellationToken.None));

        Assert.Equal($"Inventory item with ID '{missingInventoryItemId.Value}' was not found.", exception.Message);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        inventoryItemRepositoryMock.Verify(
            x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()),
            Times.Once);
        inventoryItemRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        unitOfWorkMock.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetWorkOrderById_ShouldReturnMappedDetails_WhenWorkOrderExists()
    {
        var customerId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();
        var estimateId = Guid.NewGuid();
        var details = CreateWorkOrderDetailsReadModel(
            workOrderId,
            customerId,
            estimates:
            [
                new WorkOrderEstimateReadModel(
                    Id: estimateId,
                    WorkOrderId: workOrderId,
                    Status: "Pending",
                    TotalAmount: 420m,
                    CreatedAt: DateTime.UtcNow,
                    UpdatedAt: DateTime.UtcNow,
                    InventoryLines:
                    [
                        new WorkOrderInventoryLineReadModel(
                            Id: Guid.NewGuid(),
                            EstimateId: estimateId,
                            InventoryItemId: Guid.NewGuid(),
                            DescriptionSnapshot: "Ignition coil",
                            Quantity: 2,
                            UnitCost: 80m,
                            UnitPrice: 120m,
                            TotalPrice: 240m)
                    ],
                    ServiceLines:
                    [
                        new WorkOrderServiceLineReadModel(
                            Id: Guid.NewGuid(),
                            EstimateId: estimateId,
                            ServiceId: Guid.NewGuid(),
                            DescriptionSnapshot: "Electrical diagnosis",
                            UnitPrice: 180m,
                            TotalPrice: 180m,
                            Status: "Pending",
                            StartedAt: null,
                            CompletedAt: null)
                    ])
            ]);

        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(detailsById: [details]);
        var handler = new GetWorkOrderByIdHandler(workOrderRepositoryMock.Object);

        var result = await handler.Handle(new GetWorkOrderByIdQuery(workOrderId), CancellationToken.None);

        Assert.Equal(workOrderId, result.Id);
        Assert.Equal("WaitingApproval", result.Status);
        var estimate = Assert.Single(result.Estimates);
        var inventoryLine = Assert.Single(estimate.InventoryLines);
        Assert.Equal(80m, inventoryLine.UnitCost);
    }

    [Fact]
    public async Task GetWorkOrderById_ShouldThrowNotFoundException_WhenWorkOrderDoesNotExist()
    {
        var workOrderId = Guid.NewGuid();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock();
        var handler = new GetWorkOrderByIdHandler(workOrderRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new GetWorkOrderByIdQuery(workOrderId), CancellationToken.None));

        Assert.Equal($"Work order with ID '{workOrderId}' was not found.", exception.Message);
    }

    [Fact]
    public async Task ListWorkOrders_ShouldReturnMappedPage_WhenPageRequestIsValid()
    {
        var customerId = Guid.NewGuid();
        var detailsList = Enumerable.Range(0, 6)
            .Select(index => CreateWorkOrderDetailsReadModel(
                id: Guid.NewGuid(),
                customerId: index % 2 == 0 ? customerId : Guid.NewGuid()))
            .ToList();
        var expectedWorkOrderId = detailsList[4].Id;

        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(detailsList: detailsList);
        var handler = new ListWorkOrdersHandler(workOrderRepositoryMock.Object);

        var result = await handler.Handle(
            new ListWorkOrdersQuery(Page: 2, PageSize: 2, CustomerId: customerId),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(expectedWorkOrderId, result.Items[0].Id);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        workOrderRepositoryMock.Verify(
            x => x.ListDetailsAsync(2, 2, It.IsAny<CustomerId?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListWorkOrders_ShouldThrowValidationException_WhenPageIsInvalid()
    {
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock();
        var handler = new ListWorkOrdersHandler(workOrderRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new ListWorkOrdersQuery(Page: 0, PageSize: 20),
                CancellationToken.None));

        Assert.Equal("Page must be greater than or equal to 1. Received: 0.", exception.Message);
        workOrderRepositoryMock.Verify(
            x => x.ListDetailsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CustomerId?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAverageServiceTime_ShouldThrowValidationException_WhenWindowIsInvalid()
    {
        var from = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(
            averageServiceTime: new AverageServiceTimeReadModel(CompletedWorkOrdersCount: 0, AverageDurationMinutes: null));
        var handler = new GetAverageServiceTimeHandler(workOrderRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new GetAverageServiceTimeQuery(from, from), CancellationToken.None));

        Assert.Equal("From must be earlier than To.", exception.Message);
        workOrderRepositoryMock.Verify(
            x => x.GetAverageServiceTimeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAverageServiceTime_ShouldReturnAverageDuration_WhenWindowHasCompletedWorkOrders()
    {
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(
            averageServiceTime: new AverageServiceTimeReadModel(
                CompletedWorkOrdersCount: 3,
                AverageDurationMinutes: 184.5d));
        var handler = new GetAverageServiceTimeHandler(workOrderRepositoryMock.Object);

        var result = await handler.Handle(new GetAverageServiceTimeQuery(from, to), CancellationToken.None);

        Assert.Equal(from, result.From);
        Assert.Equal(to, result.To);
        Assert.Equal(3, result.CompletedWorkOrdersCount);
        Assert.Equal(184.5d, result.AverageDurationMinutes);
        workOrderRepositoryMock.Verify(
            x => x.GetAverageServiceTimeAsync(from, to, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAverageServiceTime_ShouldReturnNullAverage_WhenWindowHasNoCompletedWorkOrders()
    {
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock(
            averageServiceTime: new AverageServiceTimeReadModel(
                CompletedWorkOrdersCount: 0,
                AverageDurationMinutes: null));
        var handler = new GetAverageServiceTimeHandler(workOrderRepositoryMock.Object);

        var result = await handler.Handle(new GetAverageServiceTimeQuery(from, to), CancellationToken.None);

        Assert.Equal(0, result.CompletedWorkOrdersCount);
        Assert.Null(result.AverageDurationMinutes);
    }

    private static Mock<IWorkOrderRepository> CreateWorkOrderRepositoryMock(
        List<WorkOrder>? initialWorkOrders = null,
        List<WorkOrderDetailsReadModel>? detailsById = null,
        List<WorkOrderDetailsReadModel>? detailsList = null,
        List<WorkOrderDetailsReadModel>? customerDetailsById = null,
        List<WorkOrderDetailsReadModel>? customerDetailsList = null,
        AverageServiceTimeReadModel? averageServiceTime = null)
    {
        var workOrders = initialWorkOrders ?? [];
        var staffDetailsById = detailsById ?? [];
        var staffDetailsList = detailsList ?? [];
        var customerDetailsByIdList = customerDetailsById ?? [];
        var customerDetailsListPage = customerDetailsList ?? [];
        var repositoryMock = new Mock<IWorkOrderRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrderId id, CancellationToken _) => workOrders.SingleOrDefault(workOrder => workOrder.Id == id));

        repositoryMock
            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrderId id, CancellationToken _) => workOrders.SingleOrDefault(workOrder => workOrder.Id == id));

        repositoryMock
            .Setup(x => x.GetDetailsByIdAsync(
                It.IsAny<WorkOrderId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrderId id, CancellationToken _) =>
                staffDetailsById.SingleOrDefault(item => item.Id == id.Value));

        repositoryMock
            .Setup(x => x.GetCustomerDetailsByIdAsync(
                It.IsAny<WorkOrderId>(),
                It.IsAny<CustomerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrderId id, CustomerId customerId, CancellationToken _) =>
                customerDetailsByIdList.SingleOrDefault(item => item.Id == id.Value && item.CustomerId == customerId.Value));

        repositoryMock
            .Setup(x => x.ListDetailsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CustomerId?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CustomerId? customerId, CancellationToken _) =>
            {
                var filtered = staffDetailsList.AsEnumerable();
                if (customerId is not null)
                {
                    filtered = filtered.Where(item => item.CustomerId == customerId.Value.Value);
                }

                var pageItems = filtered
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var totalCount = filtered.Count();
                return ((IReadOnlyList<WorkOrderDetailsReadModel>)pageItems, totalCount);
            });

        repositoryMock
            .Setup(x => x.ListCustomerDetailsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CustomerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int page, int pageSize, CustomerId customerId, CancellationToken _) =>
            {
                var items = customerDetailsListPage
                    .Where(item => item.CustomerId == customerId.Value)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var totalCount = customerDetailsListPage.Count(item => item.CustomerId == customerId.Value);
                return ((IReadOnlyList<WorkOrderDetailsReadModel>)items, totalCount);
            });

        repositoryMock
            .Setup(x => x.GetAverageServiceTimeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(averageServiceTime ?? new AverageServiceTimeReadModel(
                CompletedWorkOrdersCount: 0,
                AverageDurationMinutes: null));

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<WorkOrder>(), It.IsAny<CancellationToken>()))
            .Returns((WorkOrder workOrder, CancellationToken _) =>
            {
                workOrders.Add(workOrder);
                return Task.CompletedTask;
            });

        return repositoryMock;
    }

    private static WorkOrderDetailsReadModel CreateWorkOrderDetailsReadModel(
        Guid id,
        Guid customerId,
        IReadOnlyList<WorkOrderEstimateReadModel>? estimates = null)
    {
        return new WorkOrderDetailsReadModel(
            Id: id,
            CustomerId: customerId,
            VehicleId: Guid.NewGuid(),
            Status: "WaitingApproval",
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            Estimates: estimates ?? []);
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

        repositoryMock
            .Setup(x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()))
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
