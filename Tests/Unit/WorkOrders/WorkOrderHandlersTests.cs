using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.UseCases.AddEstimateInventoryItem;
using GarageFlow.Application.WorkOrders.UseCases.AddEstimateService;
using GarageFlow.Application.WorkOrders.UseCases.ApproveMyEstimate;
using GarageFlow.Application.WorkOrders.UseCases.CancelWorkOrder;
using GarageFlow.Application.WorkOrders.UseCases.CompleteEstimateService;
using GarageFlow.Application.WorkOrders.UseCases.CreateEstimate;
using GarageFlow.Application.WorkOrders.UseCases.CreateWorkOrder;
using GarageFlow.Application.WorkOrders.UseCases.DeliverWorkOrder;
using GarageFlow.Application.WorkOrders.UseCases.GetAverageServiceTime;
using GarageFlow.Application.WorkOrders.UseCases.GetMyWorkOrderById;
using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderById;
using GarageFlow.Application.WorkOrders.UseCases.ListMyWorkOrders;
using GarageFlow.Application.WorkOrders.UseCases.ListWorkOrders;
using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderStatus;
using GarageFlow.Application.WorkOrders.UseCases.RejectMyEstimate;
using GarageFlow.Application.WorkOrders.UseCases.StartDiagnosis;
using GarageFlow.Application.WorkOrders.UseCases.StartEstimateService;
using GarageFlow.Application.WorkOrders.UseCases.SubmitEstimate;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Customers.Entities;
using GarageFlow.Domain.Customers.Enums;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.InventoryItems.Entities;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Services.Entities;
using GarageFlow.Application.Services.Ports;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Domain.Users.ValueObjects;
using GarageFlow.Domain.Vehicles.Entities;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.Events;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.WorkOrders.ReadModels;
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
    public async Task CreateWorkOrder_ShouldCreateReceivedWorkOrder_WhenVehicleBelongsToCustomer()
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
            vehicleRepositoryMock.Object);

        var result = await handler.Handle(new CreateWorkOrderCommand(customer.Id.Value, vehicle.Id.Value), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(customer.Id.Value, result.CustomerId);
        Assert.Equal(vehicle.Id.Value, result.VehicleId);
        Assert.Equal("Received", result.Status);
        Assert.Single(workOrders);
        Assert.Equal(WorkOrderStatus.Received, workOrders[0].Status);
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
            vehicleRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new CreateWorkOrderCommand(customer.Id.Value, vehicle.Id.Value), CancellationToken.None));

        Assert.Equal("Vehicle does not belong to the customer.", exception.Message);
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
            inventoryItemRepositoryMock.Object);

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
    }

    [Fact]
    public async Task AddEstimateInventoryItem_ShouldUseMutationRepositoriesForStockReservation()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        var inventoryItem = new InventoryItemBuilder().WithStockQuantity(10).Build();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock([inventoryItem]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var sequence = new MockSequence();

        unitOfWorkMock

            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock

            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        inventoryItemRepositoryMock

            .Setup(x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        unitOfWorkMock

            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AddEstimateInventoryItemHandler(
            workOrderRepositoryMock.Object,
            inventoryItemRepositoryMock.Object);

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
            inventoryItemRepositoryMock.Object);

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
            inventoryItemRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new AddEstimateInventoryItemCommand(
                    workOrder.Id.Value,
                    estimateId.Value,
                    inventoryItem.Id.Value,
                    Quantity: 1),
                CancellationToken.None));

        Assert.Equal("In-progress work orders cannot be changed.", exception.Message);
        Assert.Equal(6, inventoryItem.StockQuantity.Value);
        inventoryItemRepositoryMock.Verify(
            x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            inventoryItemRepositoryMock.Object);

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
            serviceRepositoryMock.Object);

        var result = await handler.Handle(
            new AddEstimateServiceCommand(workOrder.Id.Value, estimate.Id.Value, service.Id.Value),
            CancellationToken.None);

        Assert.Equal(estimate.Id.Value, result.EstimateId);
        Assert.Equal(service.Id.Value, result.ServiceId);
        Assert.Equal("Front suspension alignment", result.Description);
        Assert.Equal(180m, result.UnitPrice);
        Assert.Equal(180m, result.TotalPrice);
    }

    [Fact]
    public async Task AddEstimateService_ShouldUseMutationRepositoryForEstimateApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        var service = new ServiceBuilder().WithPrice(120m).Build();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var serviceRepositoryMock = CreateServiceRepositoryMock([service]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var sequence = new MockSequence();

        unitOfWorkMock

            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock

            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        serviceRepositoryMock

            .Setup(x => x.GetByIdAsync(It.IsAny<ServiceId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        unitOfWorkMock

            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AddEstimateServiceHandler(
            workOrderRepositoryMock.Object,
            serviceRepositoryMock.Object);

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
            serviceRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(
                new AddEstimateServiceCommand(workOrder.Id.Value, estimateId.Value, service.Id.Value),
                CancellationToken.None));

        Assert.Equal("In-progress work orders cannot be changed.", exception.Message);
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
        var handler = new CreateEstimateHandler(workOrderRepositoryMock.Object);

        var result = await handler.Handle(new CreateEstimateCommand(workOrder.Id.Value), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(workOrder.Id.Value, result.WorkOrderId);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(0m, result.TotalAmount);
    }

    [Fact]
    public async Task CreateEstimate_ShouldUseMutationRepositoryForEstimateApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var sequence = new MockSequence();

        unitOfWorkMock

            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock

            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        unitOfWorkMock

            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateEstimateHandler(workOrderRepositoryMock.Object);

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
        var handler = new SubmitEstimateHandler(workOrderRepositoryMock.Object);

        var result = await handler.Handle(new SubmitEstimateCommand(workOrder.Id.Value, estimate.Id.Value), CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.WaitingApproval, workOrder.Status);
        Assert.Equal(EstimateStatus.Pending, workOrder.Estimates.Single().Status);
    }

    [Fact]
    public async Task SubmitEstimate_ShouldUseMutationRepositoryForEstimateMutation()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);

        workOrderRepositoryMock

            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        var handler = new SubmitEstimateHandler(workOrderRepositoryMock.Object);

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
        var handler = new StartEstimateServiceHandler(workOrderRepositoryMock.Object);

        await handler.Handle(
            new StartEstimateServiceCommand(workOrder.Id.Value, estimate.Id.Value, serviceLine.Id.Value),
            CancellationToken.None);

        Assert.Equal(EstimateServiceLineStatus.InProgress, serviceLine.Status);
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        workOrderRepositoryMock.Verify(
            x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()),
            Times.Once);
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
        var handler = new CompleteEstimateServiceHandler(workOrderRepositoryMock.Object);

        await handler.Handle(
            new CompleteEstimateServiceCommand(workOrder.Id.Value, estimate.Id.Value, serviceLine.Id.Value),
            CancellationToken.None);

        Assert.Equal(EstimateServiceLineStatus.Completed, serviceLine.Status);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
    }

    [Fact]
    public async Task StartEstimateService_ShouldRollback_WhenWorkOrderIsMissing()
    {
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new StartEstimateServiceHandler(workOrderRepositoryMock.Object);
        var workOrderId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new StartEstimateServiceCommand(workOrderId, Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None));

        Assert.Equal($"Work order with ID '{workOrderId}' was not found.", exception.Message);
    }

    [Fact]
    public async Task CompleteEstimateService_ShouldRollback_WhenWorkOrderIsMissing()
    {
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CompleteEstimateServiceHandler(workOrderRepositoryMock.Object);
        var workOrderId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new CompleteEstimateServiceCommand(workOrderId, Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None));

        Assert.Equal($"Work order with ID '{workOrderId}' was not found.", exception.Message);
    }

    [Fact]
    public async Task StartDiagnosis_ShouldTransitionToDiagnosing_WhenWorkOrderIsCreated()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new StartDiagnosisHandler(workOrderRepositoryMock.Object);

        var result = await handler.Handle(new StartDiagnosisCommand(workOrder.Id.Value), CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.Diagnosing, workOrder.Status);
    }

    [Fact]
    public async Task CancelWorkOrder_ShouldTransitionToCancelled_WhenWorkOrderIsCreated()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new CancelWorkOrderHandler(
            workOrderRepositoryMock.Object,
            CreateEstimateDecisionProcessor());

        var result = await handler.Handle(new CancelWorkOrderCommand(workOrder.Id.Value), CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.Cancelled, workOrder.Status);
    }

    [Fact]
    public async Task CancelWorkOrder_ShouldRestoreReservedStockExactlyOnce_UsingMutationLock()
    {
        var inventoryItem = new InventoryItemBuilder().WithStockQuantity(8).Build();
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        workOrder.AddInventoryLine(
            estimate.Id,
            inventoryItem.Id,
            Description.Create("Reserved battery"),
            EstimateItemQuantity.Create(2),
            Price.Create(100m),
            Price.Create(150m));
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.SubmitEstimate(estimate.Id);
        workOrder.ApproveEstimate(estimate.Id);
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock([inventoryItem]);
        var handler = new CancelWorkOrderHandler(
            workOrderRepositoryMock.Object,
            CreateEstimateDecisionProcessor(inventoryItemRepositoryMock.Object));

        await handler.Handle(new CancelWorkOrderCommand(workOrder.Id.Value), CancellationToken.None);
        await handler.Handle(new CancelWorkOrderCommand(workOrder.Id.Value), CancellationToken.None);

        Assert.Equal(WorkOrderStatus.Cancelled, workOrder.Status);
        Assert.Equal(10, inventoryItem.StockQuantity.Value);
        workOrderRepositoryMock.Verify(
            repository => repository.GetByIdForEstimateMutationAsync(
                workOrder.Id,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        workOrderRepositoryMock.Verify(
            repository => repository.GetByIdAsync(
                It.IsAny<WorkOrderId>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        inventoryItemRepositoryMock.Verify(
            repository => repository.GetByIdForStockReservationAsync(
                inventoryItem.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeliverWorkOrder_ShouldTransitionToDelivered_WhenWorkOrderIsCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildCompleted();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeliverWorkOrderHandler(workOrderRepositoryMock.Object);

        var result = await handler.Handle(new DeliverWorkOrderCommand(workOrder.Id.Value), CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.Delivered, workOrder.Status);
    }

    [Fact]
    public async Task DeliverWorkOrder_ShouldRollback_WhenWorkOrderIsNotCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var workOrderRepositoryMock = CreateWorkOrderRepositoryMock([workOrder]);
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new DeliverWorkOrderHandler(workOrderRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            async () => await handler.Handle(new DeliverWorkOrderCommand(workOrder.Id.Value), CancellationToken.None));

        Assert.Equal("Only completed work orders can be delivered.", exception.Message);
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
        var inventoryItemRepositoryMock = CreateInventoryItemRepositoryMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ApproveMyEstimateHandler(
            userRepositoryMock.Object,
            CreateCustomerAccessRepositoryMock().Object,
            workOrderRepositoryMock.Object);

        var result = await handler.Handle(
            new ApproveMyEstimateCommand(user.Id.Value, workOrder.Id.Value, estimate.Id.Value),
            CancellationToken.None);

        Assert.Equal(MediatorUnit.Value, result);
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        Assert.Equal(EstimateStatus.Approved, workOrder.Estimates.Single().Status);
        inventoryItemRepositoryMock.Verify(
            repository => repository.GetByIdForStockReservationAsync(
                It.IsAny<InventoryItemId>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApproveMyEstimate_ShouldUseMutationRepositoryForEstimateApproval()
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

            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock

            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        unitOfWorkMock

            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ApproveMyEstimateHandler(
            userRepositoryMock.Object,
            CreateCustomerAccessRepositoryMock().Object,
            workOrderRepositoryMock.Object);

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
            CreateCustomerAccessRepositoryMock().Object,
            workOrderRepositoryMock.Object);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(
                new ApproveMyEstimateCommand(user.Id.Value, workOrder.Id.Value, estimate.Id.Value),
                CancellationToken.None));

        Assert.Equal($"Work order with ID '{workOrder.Id.Value}' was not found.", exception.Message);
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
            CreateCustomerAccessRepositoryMock().Object,
            workOrderRepositoryMock.Object,
            CreateEstimateDecisionProcessor(inventoryItemRepositoryMock.Object));

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
    }

    [Fact]
    public async Task RejectMyEstimate_ShouldUseMutationRepositoryForEstimateApproval()
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

            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        workOrderRepositoryMock

            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workOrder);

        inventoryItemRepositoryMock

            .Setup(x => x.GetByIdForStockReservationAsync(It.IsAny<InventoryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        unitOfWorkMock

            .Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RejectMyEstimateHandler(
            userRepositoryMock.Object,
            CreateCustomerAccessRepositoryMock().Object,
            workOrderRepositoryMock.Object,
            CreateEstimateDecisionProcessor(inventoryItemRepositoryMock.Object));

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
            CreateCustomerAccessRepositoryMock().Object,
            workOrderRepositoryMock.Object,
            CreateEstimateDecisionProcessor(inventoryItemRepositoryMock.Object));

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
        var workOrderQueriesMock = CreateWorkOrderQueriesMock(customerDetailsById: [details]);
        var handler = new GetMyWorkOrderByIdHandler(
            userRepositoryMock.Object,
            CreateCustomerAccessRepositoryMock().Object,
            workOrderQueriesMock.Object);

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
        var workOrderQueriesMock = CreateWorkOrderQueriesMock(customerDetailsList: pagedDetails);
        var handler = new ListMyWorkOrdersHandler(
            userRepositoryMock.Object,
            CreateCustomerAccessRepositoryMock().Object,
            workOrderQueriesMock.Object);

        var result = await handler.Handle(
            new ListMyWorkOrdersQuery(user.Id.Value, Page: 2, PageSize: 5),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(expectedWorkOrderId, result.Items[0].Id);
        Assert.Equal(6, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        workOrderQueriesMock.Verify(
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
        var workOrderQueriesMock = CreateWorkOrderQueriesMock();
        var handler = new ListMyWorkOrdersHandler(
            userRepositoryMock.Object,
            CreateCustomerAccessRepositoryMock().Object,
            workOrderQueriesMock.Object);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await handler.Handle(
                new ListMyWorkOrdersQuery(user.Id.Value),
                CancellationToken.None));

        Assert.Equal("Authenticated user is not a customer user.", exception.Message);
        workOrderQueriesMock.Verify(
            x => x.ListCustomerDetailsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CustomerId>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ListMyWorkOrders_ShouldThrowUnauthorizedAccessException_WhenLinkedCustomerIsMissing()
    {
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(CustomerId.New())
            .WithEmail("customer.missing@example.com")
            .Build();

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderQueriesMock = CreateWorkOrderQueriesMock();
        var handler = new ListMyWorkOrdersHandler(
            userRepositoryMock.Object,
            CreateCustomerAccessRepositoryMock(customerExists: false).Object,
            workOrderQueriesMock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(
                new ListMyWorkOrdersQuery(user.Id.Value),
                CancellationToken.None).AsTask());

        workOrderQueriesMock.Verify(
            x => x.ListCustomerDetailsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CustomerId>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ListMyWorkOrders_ShouldThrowUnauthorizedAccessException_WhenCustomerIsSuspended()
    {
        var customer = new CustomerBuilder().Build();
        customer.ChangeStatus(CustomerStatus.Suspended);
        var user = new UserBuilder()
            .WithRole(UserRole.Customer)
            .WithCustomerId(customer.Id)
            .WithEmail("customer.suspended@example.com")
            .Build();

        var userRepositoryMock = CreateUserRepositoryMock([user]);
        var workOrderQueriesMock = CreateWorkOrderQueriesMock();
        var handler = new ListMyWorkOrdersHandler(
            userRepositoryMock.Object,
            CreateCustomerAccessRepositoryMock(customer).Object,
            workOrderQueriesMock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(
                new ListMyWorkOrdersQuery(user.Id.Value),
                CancellationToken.None).AsTask());

        workOrderQueriesMock.Verify(
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
            CreateCustomerAccessRepositoryMock().Object,
            workOrderRepositoryMock.Object,
            CreateEstimateDecisionProcessor(inventoryItemRepositoryMock.Object));

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

        var workOrderQueriesMock = CreateWorkOrderQueriesMock(detailsById: [details]);
        var handler = new GetWorkOrderByIdHandler(workOrderQueriesMock.Object);

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
        var workOrderQueriesMock = CreateWorkOrderQueriesMock();
        var handler = new GetWorkOrderByIdHandler(workOrderQueriesMock.Object);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new GetWorkOrderByIdQuery(workOrderId), CancellationToken.None));

        Assert.Equal($"Work order with ID '{workOrderId}' was not found.", exception.Message);
    }

    [Fact]
    public async Task GetWorkOrderStatus_ShouldReturnFocusedStatus_WhenWorkOrderExists()
    {
        var workOrderId = Guid.NewGuid();
        var updatedAt = new DateTime(2026, 7, 12, 12, 30, 0, DateTimeKind.Utc);
        var status = new WorkOrderStatusReadModel(workOrderId, "Diagnosing", updatedAt);
        var workOrderQueriesMock = CreateWorkOrderQueriesMock(statusById: [status]);
        var handler = new GetWorkOrderStatusHandler(workOrderQueriesMock.Object);

        var result = await handler.Handle(new GetWorkOrderStatusQuery(workOrderId), CancellationToken.None);

        Assert.Equal(workOrderId, result.Id);
        Assert.Equal("Diagnosing", result.Status);
        Assert.Equal(updatedAt, result.UpdatedAt);
    }

    [Fact]
    public async Task GetWorkOrderStatus_ShouldThrowNotFoundException_WhenWorkOrderDoesNotExist()
    {
        var workOrderId = Guid.NewGuid();
        var handler = new GetWorkOrderStatusHandler(CreateWorkOrderQueriesMock().Object);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            async () => await handler.Handle(new GetWorkOrderStatusQuery(workOrderId), CancellationToken.None));

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

        var workOrderQueriesMock = CreateWorkOrderQueriesMock(detailsList: detailsList);
        var handler = new ListWorkOrdersHandler(workOrderQueriesMock.Object);

        var result = await handler.Handle(
            new ListWorkOrdersQuery(Page: 2, PageSize: 2, CustomerId: customerId),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(expectedWorkOrderId, result.Items[0].Id);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        workOrderQueriesMock.Verify(
            x => x.ListActiveDetailsAsync(2, 2, It.IsAny<CustomerId?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListWorkOrders_ShouldThrowValidationException_WhenPageIsInvalid()
    {
        var workOrderQueriesMock = CreateWorkOrderQueriesMock();
        var handler = new ListWorkOrdersHandler(workOrderQueriesMock.Object);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(
                new ListWorkOrdersQuery(Page: 0, PageSize: 20),
                CancellationToken.None));

        Assert.Equal("Page must be greater than or equal to 1. Received: 0.", exception.Message);
        workOrderQueriesMock.Verify(
            x => x.ListActiveDetailsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CustomerId?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAverageServiceTime_ShouldThrowValidationException_WhenWindowIsInvalid()
    {
        var from = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var workOrderQueriesMock = CreateWorkOrderQueriesMock(
            averageServiceTime: new AverageServiceTimeReadModel(CompletedServicesCount: 0, AverageDurationMinutes: null));
        var handler = new GetAverageServiceTimeHandler(workOrderQueriesMock.Object);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new GetAverageServiceTimeQuery(from, from), CancellationToken.None));

        Assert.Equal("From must be earlier than To.", exception.Message);
        workOrderQueriesMock.Verify(
            x => x.GetAverageServiceTimeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<ServiceId?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAverageServiceTime_ShouldThrowValidationException_WhenServiceIdIsEmpty()
    {
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);
        var workOrderQueriesMock = CreateWorkOrderQueriesMock();
        var handler = new GetAverageServiceTimeHandler(workOrderQueriesMock.Object);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            async () => await handler.Handle(new GetAverageServiceTimeQuery(from, to, Guid.Empty), CancellationToken.None));

        Assert.Equal("Service identifier cannot be empty.", exception.Message);
        workOrderQueriesMock.Verify(
            x => x.GetAverageServiceTimeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<ServiceId?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAverageServiceTime_ShouldReturnAverageDuration_WhenWindowHasCompletedServices()
    {
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);
        var serviceId = Guid.NewGuid();
        var workOrderQueriesMock = CreateWorkOrderQueriesMock(
            averageServiceTime: new AverageServiceTimeReadModel(
                CompletedServicesCount: 3,
                AverageDurationMinutes: 184.5d));
        var handler = new GetAverageServiceTimeHandler(workOrderQueriesMock.Object);

        var result = await handler.Handle(new GetAverageServiceTimeQuery(from, to, serviceId), CancellationToken.None);

        Assert.Equal(from, result.From);
        Assert.Equal(to, result.To);
        Assert.Equal(serviceId, result.ServiceId);
        Assert.Equal(3, result.CompletedServicesCount);
        Assert.Equal(184.5d, result.AverageDurationMinutes);
        workOrderQueriesMock.Verify(
            x => x.GetAverageServiceTimeAsync(from, to, It.Is<ServiceId?>(id => id!.Value.Value == serviceId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAverageServiceTime_ShouldReturnNullAverage_WhenWindowHasNoCompletedServices()
    {
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);
        var workOrderQueriesMock = CreateWorkOrderQueriesMock(
            averageServiceTime: new AverageServiceTimeReadModel(
                CompletedServicesCount: 0,
                AverageDurationMinutes: null));
        var handler = new GetAverageServiceTimeHandler(workOrderQueriesMock.Object);

        var result = await handler.Handle(new GetAverageServiceTimeQuery(from, to), CancellationToken.None);

        Assert.Equal(0, result.CompletedServicesCount);
        Assert.Null(result.AverageDurationMinutes);
    }

    private static Mock<IWorkOrderRepository> CreateWorkOrderRepositoryMock(
        List<WorkOrder>? initialWorkOrders = null)
    {
        var workOrders = initialWorkOrders ?? [];
        var repositoryMock = new Mock<IWorkOrderRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrderId id, CancellationToken _) => workOrders.SingleOrDefault(workOrder => workOrder.Id == id));

        repositoryMock
            .Setup(x => x.GetByIdForEstimateMutationAsync(It.IsAny<WorkOrderId>(), It.IsAny<CancellationToken>()))
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

    private static Mock<IWorkOrderQueries> CreateWorkOrderQueriesMock(
        List<WorkOrderDetailsReadModel>? detailsById = null,
        List<WorkOrderDetailsReadModel>? detailsList = null,
        List<WorkOrderDetailsReadModel>? customerDetailsById = null,
        List<WorkOrderDetailsReadModel>? customerDetailsList = null,
        AverageServiceTimeReadModel? averageServiceTime = null,
        List<WorkOrderStatusReadModel>? statusById = null)
    {
        var staffDetailsById = detailsById ?? [];
        var staffDetailsList = detailsList ?? [];
        var customerDetailsByIdList = customerDetailsById ?? [];
        var customerDetailsListPage = customerDetailsList ?? [];
        var statusByIdList = statusById ?? [];
        var queriesMock = new Mock<IWorkOrderQueries>();

        queriesMock
            .Setup(x => x.GetDetailsByIdAsync(
                It.IsAny<WorkOrderId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrderId id, CancellationToken _) =>
                staffDetailsById.SingleOrDefault(item => item.Id == id.Value));

        queriesMock
            .Setup(x => x.GetCustomerDetailsByIdAsync(
                It.IsAny<WorkOrderId>(),
                It.IsAny<CustomerId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrderId id, CustomerId customerId, CancellationToken _) =>
                customerDetailsByIdList.SingleOrDefault(item => item.Id == id.Value && item.CustomerId == customerId.Value));

        queriesMock
            .Setup(x => x.ListActiveDetailsAsync(
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

        queriesMock
            .Setup(x => x.GetStatusByIdAsync(
                It.IsAny<WorkOrderId>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkOrderId id, CancellationToken _) =>
                statusByIdList.SingleOrDefault(item => item.Id == id.Value));

        queriesMock
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

        queriesMock
            .Setup(x => x.GetAverageServiceTimeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<ServiceId?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(averageServiceTime ?? new AverageServiceTimeReadModel(
                CompletedServicesCount: 0,
                AverageDurationMinutes: null));

        return queriesMock;
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

    private static Mock<ICustomerRepository> CreateCustomerAccessRepositoryMock(
        Customer? customer = null,
        bool customerExists = true)
    {
        var accessibleCustomer = customerExists
            ? customer ?? new CustomerBuilder().Build()
            : null;
        var repositoryMock = new Mock<ICustomerRepository>();

        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<CustomerId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessibleCustomer);

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

    private static EstimateDecisionProcessor CreateEstimateDecisionProcessor(
        IInventoryItemRepository? inventoryItemRepository = null)
    {
        return new EstimateDecisionProcessor(
            inventoryItemRepository ?? CreateInventoryItemRepositoryMock().Object);
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
