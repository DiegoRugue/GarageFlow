using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.Events;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.Tests.Shared.WorkOrders;

namespace GarageFlow.Tests.Unit.WorkOrders;

public class WorkOrderTests
{
    [Fact]
    public void Create_ShouldCreateWorkOrder_WithCreatedStatus()
    {
        var workOrder = WorkOrder.Create(CustomerId.New(), VehicleId.New());

        Assert.Equal(WorkOrderStatus.Created, workOrder.Status);
        Assert.Empty(workOrder.Estimates);
        var createdEvent = Assert.Single(workOrder.DomainEvents.OfType<WorkOrderCreated>());
        Assert.Equal(workOrder.Id, createdEvent.WorkOrderId);
        Assert.Equal(workOrder.CustomerId, createdEvent.CustomerId);
        Assert.Equal(workOrder.VehicleId, createdEvent.VehicleId);
        Assert.Equal(WorkOrderStatus.Created, createdEvent.Status);
    }

    [Fact]
    public void SubmitEstimate_ShouldThrowBusinessRuleViolationException_WhenEstimateIsEmpty()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        var statusChangedEventsBefore = workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count();
        var submittedEventsBefore = workOrder.DomainEvents.OfType<EstimateSubmitted>().Count();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.SubmitEstimate(estimate.Id));

        Assert.Equal("Estimate must contain at least one service line before submission.", exception.Message);
        Assert.Equal(WorkOrderStatus.Created, workOrder.Status);
        Assert.Equal(EstimateStatus.Draft, estimate.Status);
        Assert.Equal(statusChangedEventsBefore, workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count());
        Assert.Equal(submittedEventsBefore, workOrder.DomainEvents.OfType<EstimateSubmitted>().Count());
    }

    [Fact]
    public void ApproveEstimate_ShouldApprovePendingEstimate_AndMoveWorkOrderToApproved()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();
        var estimateId = workOrder.Estimates.Single().Id;

        workOrder.ApproveEstimate(estimateId);

        Assert.Equal(WorkOrderStatus.Approved, workOrder.Status);
        var estimate = workOrder.Estimates.Single();
        Assert.Equal(EstimateStatus.Approved, estimate.Status);
        Assert.Contains(workOrder.DomainEvents, domainEvent => domainEvent is WorkOrderStatusChanged statusChanged && statusChanged.NewStatus == WorkOrderStatus.Approved);
        Assert.Single(workOrder.DomainEvents.OfType<EstimateApproved>());
    }

    [Fact]
    public void RejectEstimate_ShouldRejectPendingEstimate_AndKeepWorkOrderWaitingApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();
        var estimateId = workOrder.Estimates.Single().Id;

        var rejectedEstimate = workOrder.RejectEstimate(estimateId);

        Assert.Equal(WorkOrderStatus.WaitingApproval, workOrder.Status);
        Assert.Equal(EstimateStatus.Rejected, rejectedEstimate.Status);
        Assert.Same(workOrder.Estimates.Single(), rejectedEstimate);
        Assert.Single(workOrder.DomainEvents.OfType<EstimateRejected>());
    }

    [Fact]
    public void Complete_ShouldThrowBusinessRuleViolationException_WhenNoEstimateIsApproved()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.Complete());

        Assert.Equal("Work order requires exactly one approved estimate before completion.", exception.Message);
    }

    [Fact]
    public void Complete_ShouldCompleteWorkOrder_WhenEstimateIsApproved()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        workOrder.StartWork();

        workOrder.Complete();

        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Contains(workOrder.DomainEvents, domainEvent => domainEvent is WorkOrderStatusChanged statusChanged && statusChanged.NewStatus == WorkOrderStatus.Completed);
    }

    [Fact]
    public void AddInventoryLine_ShouldCalculateEstimateTotal()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();

        workOrder.AddInventoryLine(
            estimate.Id,
            InventoryItemId.New(),
            Description.Create("Oil filter"),
            EstimateItemQuantity.Create(3),
            Price.Create(15.00m),
            Price.Create(25.00m));
        workOrder.AddServiceLine(
            estimate.Id,
            ServiceId.New(),
            Description.Create("Installation labor"),
            Price.Create(50.00m));

        Assert.Equal(125.00m, estimate.TotalAmount.Value);
        var inventoryLine = Assert.Single(estimate.InventoryLines);
        Assert.Equal("Oil filter", inventoryLine.DescriptionSnapshot.Value);
        Assert.Equal(75.00m, inventoryLine.TotalPrice.Value);
        var serviceLine = Assert.Single(estimate.ServiceLines);
        Assert.Equal("Installation labor", serviceLine.DescriptionSnapshot.Value);
        Assert.Equal(50.00m, serviceLine.TotalPrice.Value);
        Assert.Single(workOrder.DomainEvents.OfType<EstimateInventoryLineAdded>());
        Assert.Single(workOrder.DomainEvents.OfType<EstimateServiceLineAdded>());
    }

    [Fact]
    public void StartWork_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsWaitingApproval()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.StartWork());

        Assert.Equal("Work order cannot start while waiting for customer approval.", exception.Message);
    }

    [Fact]
    public void StartDiagnosis_ShouldNotSetServiceTiming()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();

        workOrder.StartDiagnosis();

        Assert.Equal(WorkOrderStatus.Diagnosing, workOrder.Status);
        Assert.Null(workOrder.StartedAt);
        Assert.Null(workOrder.CompletedAt);
    }

    [Fact]
    public void StartWork_ShouldSetStartedAt_WhenTransitioningToInProgress()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var beforeStart = DateTime.UtcNow;

        workOrder.StartWork();

        var afterStart = DateTime.UtcNow;
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        Assert.NotNull(workOrder.StartedAt);
        Assert.InRange(workOrder.StartedAt.Value, beforeStart, afterStart);
        Assert.Null(workOrder.CompletedAt);
    }

    [Fact]
    public void Complete_ShouldSetCompletedAt_WhenTransitioningToCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        workOrder.StartWork();
        var beforeComplete = DateTime.UtcNow;

        workOrder.Complete();

        var afterComplete = DateTime.UtcNow;
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.NotNull(workOrder.StartedAt);
        Assert.NotNull(workOrder.CompletedAt);
        Assert.InRange(workOrder.CompletedAt.Value, beforeComplete, afterComplete);
        Assert.True(workOrder.CompletedAt.Value >= workOrder.StartedAt.Value);
    }

    [Fact]
    public void StartWork_CalledTwice_ShouldNotOverwriteStartedAt_OrDuplicateInProgressTransitionEvent()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();

        workOrder.StartWork();
        var originalStartedAt = workOrder.StartedAt;
        var inProgressTransitionsBeforeSecondCall = workOrder.DomainEvents
            .OfType<WorkOrderStatusChanged>()
            .Count(domainEvent => domainEvent.NewStatus == WorkOrderStatus.InProgress);

        workOrder.StartWork();

        var inProgressTransitionsAfterSecondCall = workOrder.DomainEvents
            .OfType<WorkOrderStatusChanged>()
            .Count(domainEvent => domainEvent.NewStatus == WorkOrderStatus.InProgress);
        Assert.Equal(originalStartedAt, workOrder.StartedAt);
        Assert.Equal(inProgressTransitionsBeforeSecondCall, inProgressTransitionsAfterSecondCall);
    }

    [Fact]
    public void Complete_CalledTwice_ShouldNotOverwriteCompletedAt_OrDuplicateCompletedTransitionEvent()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        workOrder.StartWork();
        workOrder.Complete();
        var originalCompletedAt = workOrder.CompletedAt;
        var completedTransitionsBeforeSecondCall = workOrder.DomainEvents
            .OfType<WorkOrderStatusChanged>()
            .Count(domainEvent => domainEvent.NewStatus == WorkOrderStatus.Completed);

        workOrder.Complete();

        var completedTransitionsAfterSecondCall = workOrder.DomainEvents
            .OfType<WorkOrderStatusChanged>()
            .Count(domainEvent => domainEvent.NewStatus == WorkOrderStatus.Completed);
        Assert.Equal(originalCompletedAt, workOrder.CompletedAt);
        Assert.Equal(completedTransitionsBeforeSecondCall, completedTransitionsAfterSecondCall);
    }

    [Fact]
    public void StartWork_ShouldNotSetStartedAt_WhenTransitionIsRejected()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.StartWork());

        Assert.Equal("Work order cannot start while waiting for customer approval.", exception.Message);
        Assert.Equal(WorkOrderStatus.WaitingApproval, workOrder.Status);
        Assert.Null(workOrder.StartedAt);
        Assert.Null(workOrder.CompletedAt);
    }

    [Fact]
    public void Deliver_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsNotCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.Deliver());

        Assert.Equal("Only completed work orders can be delivered.", exception.Message);
    }

    [Fact]
    public void CreateEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildCompleted();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.CreateEstimate());

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
    }

    [Fact]
    public void CreateEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsDelivered()
    {
        var workOrder = new WorkOrderBuilder().BuildDelivered();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.CreateEstimate());

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
    }

    [Fact]
    public void CreateEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCancelled()
    {
        var workOrder = new WorkOrderBuilder().BuildCancelled();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.CreateEstimate());

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
    }

    [Fact]
    public void CreateEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsApproved()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.CreateEstimate());

        Assert.Equal("Approved or in-progress work orders cannot be changed.", exception.Message);
    }

    [Fact]
    public void CreateEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsInProgress()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        workOrder.StartWork();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.CreateEstimate());

        Assert.Equal("Approved or in-progress work orders cannot be changed.", exception.Message);
    }

    [Fact]
    public void ApproveEstimate_ShouldThrowBusinessRuleViolationException_WhenEstimateIsRejected()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();
        var estimateId = workOrder.Estimates.Single().Id;
        workOrder.RejectEstimate(estimateId);

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.ApproveEstimate(estimateId));

        Assert.Equal("Only pending estimates can be approved.", exception.Message);
    }

    [Fact]
    public void RejectEstimate_ShouldThrowBusinessRuleViolationException_WhenEstimateIsApproved()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimateId = workOrder.Estimates.Single().Id;

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.RejectEstimate(estimateId));

        Assert.Equal("Only pending estimates can be rejected.", exception.Message);
    }

    [Fact]
    public void AddServiceLine_ShouldThrowBusinessRuleViolationException_WhenEstimateIsApproved()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimateId = workOrder.Estimates.Single().Id;

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.AddServiceLine(
            estimateId,
            ServiceId.New(),
            Description.Create("Additional labor"),
            Price.Create(30.00m)));

        Assert.Equal("Approved or in-progress work orders cannot be changed.", exception.Message);
    }

    [Fact]
    public void AddServiceLine_ShouldThrowBusinessRuleViolationException_WhenEstimateIsRejected()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();
        var estimateId = workOrder.Estimates.Single().Id;
        workOrder.RejectEstimate(estimateId);

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.AddServiceLine(
            estimateId,
            ServiceId.New(),
            Description.Create("Additional labor"),
            Price.Create(30.00m)));

        Assert.Equal("Only draft estimates can be edited.", exception.Message);
    }

    [Fact]
    public void ApproveEstimate_ShouldThrowBusinessRuleViolationException_WhenAnotherEstimateIsApproved()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var firstEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, firstEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, firstEstimate.Id);
        workOrder.SubmitEstimate(firstEstimate.Id);

        var secondEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, secondEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, secondEstimate.Id);
        workOrder.SubmitEstimate(secondEstimate.Id);
        workOrder.ApproveEstimate(firstEstimate.Id);

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.ApproveEstimate(secondEstimate.Id));

        Assert.Equal("A work order cannot have more than one approved estimate.", exception.Message);
    }

    [Fact]
    public void SubmitEstimate_ShouldSetEstimateStatusToPending()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);

        workOrder.SubmitEstimate(estimate.Id);

        Assert.Equal(EstimateStatus.Pending, estimate.Status);
        Assert.Equal(WorkOrderStatus.WaitingApproval, workOrder.Status);
        var submittedEvent = Assert.Single(workOrder.DomainEvents.OfType<EstimateSubmitted>());
        Assert.Equal(estimate.TotalAmount.Value, submittedEvent.TotalAmount);
    }

    [Fact]
    public void CreateEstimate_ShouldCreateDraftEstimate()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();

        var estimate = workOrder.CreateEstimate();

        Assert.Equal(EstimateStatus.Draft, estimate.Status);
        Assert.Single(workOrder.Estimates);
        Assert.Single(workOrder.DomainEvents.OfType<EstimateCreated>());
    }

    [Fact]
    public void AddInventoryLine_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCancelled()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        workOrder.Cancel();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.AddInventoryLine(
            estimate.Id,
            InventoryItemId.New(),
            Description.Create("Cancelled test line"),
            EstimateItemQuantity.Create(1),
            Price.Create(10.00m),
            Price.Create(20.00m)));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
    }

    [Fact]
    public void AddInventoryLine_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsApproved()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimateId = workOrder.Estimates.Single().Id;

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.AddInventoryLine(
            estimateId,
            InventoryItemId.New(),
            Description.Create("Approved status line"),
            EstimateItemQuantity.Create(1),
            Price.Create(10.00m),
            Price.Create(20.00m)));

        Assert.Equal("Approved or in-progress work orders cannot be changed.", exception.Message);
    }

    [Fact]
    public void AddInventoryLine_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsInProgress()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        workOrder.StartWork();
        var estimateId = workOrder.Estimates.Single().Id;

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.AddInventoryLine(
            estimateId,
            InventoryItemId.New(),
            Description.Create("In-progress status line"),
            EstimateItemQuantity.Create(1),
            Price.Create(10.00m),
            Price.Create(20.00m)));

        Assert.Equal("Approved or in-progress work orders cannot be changed.", exception.Message);
    }

    [Fact]
    public void AddServiceLine_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsInProgress()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        workOrder.StartWork();
        var estimateId = workOrder.Estimates.Single().Id;

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.AddServiceLine(
            estimateId,
            ServiceId.New(),
            Description.Create("In-progress additional labor"),
            Price.Create(30.00m)));

        Assert.Equal("Approved or in-progress work orders cannot be changed.", exception.Message);
    }

    [Fact]
    public void AddServiceLine_ShouldThrowNotFoundException_WhenEstimateDoesNotExist()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var missingEstimateId = EstimateId.New();

        var exception = Assert.Throws<NotFoundException>(() => workOrder.AddServiceLine(
            missingEstimateId,
            ServiceId.New(),
            Description.Create("Missing estimate line"),
            Price.Create(10.00m)));

        Assert.Equal($"Estimate with ID '{missingEstimateId.Value}' was not found.", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenCustomerIdIsEmpty()
    {
        var exception = Assert.Throws<ValidationException>(() => WorkOrder.Create(default, VehicleId.New()));

        Assert.Equal("Customer identifier cannot be empty.", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenVehicleIdIsEmpty()
    {
        var exception = Assert.Throws<ValidationException>(() => WorkOrder.Create(CustomerId.New(), default));

        Assert.Equal("Vehicle identifier cannot be empty.", exception.Message);
    }

    [Fact]
    public void EstimateItemQuantity_Create_ShouldThrowValidationException_WhenValueIsLessThanOrEqualToZero()
    {
        var exception = Assert.Throws<ValidationException>(() => EstimateItemQuantity.Create(0));

        Assert.Equal("Estimate item quantity must be greater than zero.", exception.Message);
    }

    [Fact]
    public void EstimateItemQuantity_ToString_ShouldUseInvariantCulture()
    {
        var quantity = EstimateItemQuantity.Create(12);

        Assert.Equal("12", quantity.ToString());
    }

    [Fact]
    public void EstimateItemQuantity_ShouldSupportImplicitIntConversion()
    {
        var quantity = EstimateItemQuantity.Create(7);

        int rawQuantity = quantity;

        Assert.Equal(7, rawQuantity);
    }

    [Fact]
    public void ApproveEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCancelled_AndKeepEstimatePending()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();
        var estimate = workOrder.Estimates.Single();
        workOrder.Cancel();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.ApproveEstimate(estimate.Id));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
        Assert.Equal(WorkOrderStatus.Cancelled, workOrder.Status);
        Assert.Equal(EstimateStatus.Pending, estimate.Status);
        Assert.Empty(workOrder.DomainEvents.OfType<EstimateApproved>());
    }

    [Fact]
    public void RejectEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCancelled_AndKeepEstimatePending()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();
        var estimate = workOrder.Estimates.Single();
        workOrder.Cancel();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.RejectEstimate(estimate.Id));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
        Assert.Equal(WorkOrderStatus.Cancelled, workOrder.Status);
        Assert.Equal(EstimateStatus.Pending, estimate.Status);
        Assert.Empty(workOrder.DomainEvents.OfType<EstimateRejected>());
    }

    [Fact]
    public void SubmitEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsApproved_AndKeepEstimateDraft()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var approvedEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, approvedEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, approvedEstimate.Id);
        workOrder.SubmitEstimate(approvedEstimate.Id);

        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.ApproveEstimate(approvedEstimate.Id);
        var submittedEventsBefore = workOrder.DomainEvents.OfType<EstimateSubmitted>().Count();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.SubmitEstimate(estimate.Id));

        Assert.Equal("Work order status 'Approved' does not allow estimate submission.", exception.Message);
        Assert.Equal(WorkOrderStatus.Approved, workOrder.Status);
        Assert.Equal(EstimateStatus.Draft, estimate.Status);
        Assert.Equal(submittedEventsBefore, workOrder.DomainEvents.OfType<EstimateSubmitted>().Count());
    }

    [Fact]
    public void SubmitEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCompleted_AndKeepEstimateDraft()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var approvedEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, approvedEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, approvedEstimate.Id);
        workOrder.SubmitEstimate(approvedEstimate.Id);

        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.ApproveEstimate(approvedEstimate.Id);
        workOrder.StartWork();
        workOrder.Complete();
        var submittedEventsBefore = workOrder.DomainEvents.OfType<EstimateSubmitted>().Count();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.SubmitEstimate(estimate.Id));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(EstimateStatus.Draft, estimate.Status);
        Assert.Equal(submittedEventsBefore, workOrder.DomainEvents.OfType<EstimateSubmitted>().Count());
    }

    [Fact]
    public void SubmitEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsDelivered_AndKeepEstimateDraft()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var approvedEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, approvedEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, approvedEstimate.Id);
        workOrder.SubmitEstimate(approvedEstimate.Id);

        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, estimate.Id);
        workOrder.ApproveEstimate(approvedEstimate.Id);
        workOrder.StartWork();
        workOrder.Complete();
        workOrder.Deliver();
        var submittedEventsBefore = workOrder.DomainEvents.OfType<EstimateSubmitted>().Count();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.SubmitEstimate(estimate.Id));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
        Assert.Equal(WorkOrderStatus.Delivered, workOrder.Status);
        Assert.Equal(EstimateStatus.Draft, estimate.Status);
        Assert.Equal(submittedEventsBefore, workOrder.DomainEvents.OfType<EstimateSubmitted>().Count());
    }

    [Fact]
    public void SubmitEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCancelled_AndKeepEstimateDraft()
    {
        var workOrder = new WorkOrderBuilder().BuildWithPendingEstimate();
        var pendingEstimate = workOrder.Estimates.Single();
        workOrder.RejectEstimate(pendingEstimate.Id);
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);
        workOrder.Cancel();
        var submittedEventsBefore = workOrder.DomainEvents.OfType<EstimateSubmitted>().Count();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.SubmitEstimate(estimate.Id));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
        Assert.Equal(WorkOrderStatus.Cancelled, workOrder.Status);
        Assert.Equal(EstimateStatus.Draft, estimate.Status);
        Assert.Equal(submittedEventsBefore, workOrder.DomainEvents.OfType<EstimateSubmitted>().Count());
    }

    [Fact]
    public void SubmitEstimate_ShouldThrowNotFoundException_WhenEstimateIsMissingOnCreated_AndKeepStatusUnchanged()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var missingEstimateId = EstimateId.New();
        var statusChangedEventsBefore = workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count();
        var submittedEventsBefore = workOrder.DomainEvents.OfType<EstimateSubmitted>().Count();

        var exception = Assert.Throws<NotFoundException>(() => workOrder.SubmitEstimate(missingEstimateId));

        Assert.Equal($"Estimate with ID '{missingEstimateId.Value}' was not found.", exception.Message);
        Assert.Equal(WorkOrderStatus.Created, workOrder.Status);
        Assert.Equal(statusChangedEventsBefore, workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count());
        Assert.Equal(submittedEventsBefore, workOrder.DomainEvents.OfType<EstimateSubmitted>().Count());
    }

    [Fact]
    public void SubmitEstimate_ShouldThrowNotFoundException_WhenEstimateIsMissingOnDiagnosing_AndKeepStatusUnchanged()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        workOrder.StartDiagnosis();
        var missingEstimateId = EstimateId.New();
        var statusChangedEventsBefore = workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count();
        var submittedEventsBefore = workOrder.DomainEvents.OfType<EstimateSubmitted>().Count();

        var exception = Assert.Throws<NotFoundException>(() => workOrder.SubmitEstimate(missingEstimateId));

        Assert.Equal($"Estimate with ID '{missingEstimateId.Value}' was not found.", exception.Message);
        Assert.Equal(WorkOrderStatus.Diagnosing, workOrder.Status);
        Assert.Equal(statusChangedEventsBefore, workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count());
        Assert.Equal(submittedEventsBefore, workOrder.DomainEvents.OfType<EstimateSubmitted>().Count());
    }

    [Fact]
    public void SubmitEstimate_ShouldThrowBusinessRuleViolationException_WhenEstimateIsEmptyOnDiagnosing_AndKeepStatusUnchanged()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        workOrder.StartDiagnosis();
        var estimate = workOrder.CreateEstimate();
        var statusChangedEventsBefore = workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count();
        var submittedEventsBefore = workOrder.DomainEvents.OfType<EstimateSubmitted>().Count();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.SubmitEstimate(estimate.Id));

        Assert.Equal("Estimate must contain at least one service line before submission.", exception.Message);
        Assert.Equal(WorkOrderStatus.Diagnosing, workOrder.Status);
        Assert.Equal(EstimateStatus.Draft, estimate.Status);
        Assert.Equal(statusChangedEventsBefore, workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count());
        Assert.Equal(submittedEventsBefore, workOrder.DomainEvents.OfType<EstimateSubmitted>().Count());
    }

    [Fact]
    public void ApproveEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCompleted_AndKeepPendingEstimateUnchanged()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var approvedEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, approvedEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, approvedEstimate.Id);
        workOrder.SubmitEstimate(approvedEstimate.Id);

        var pendingEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, pendingEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, pendingEstimate.Id);
        workOrder.SubmitEstimate(pendingEstimate.Id);

        workOrder.ApproveEstimate(approvedEstimate.Id);
        workOrder.StartWork();
        workOrder.Complete();
        var statusChangedEventsBefore = workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count();
        var approvedEventsBefore = workOrder.DomainEvents.OfType<EstimateApproved>().Count();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.ApproveEstimate(pendingEstimate.Id));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(EstimateStatus.Pending, pendingEstimate.Status);
        Assert.Equal(statusChangedEventsBefore, workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count());
        Assert.Equal(approvedEventsBefore, workOrder.DomainEvents.OfType<EstimateApproved>().Count());
    }

    [Fact]
    public void RejectEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsCompleted_AndKeepPendingEstimateUnchanged()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var approvedEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, approvedEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, approvedEstimate.Id);
        workOrder.SubmitEstimate(approvedEstimate.Id);

        var pendingEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, pendingEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, pendingEstimate.Id);
        workOrder.SubmitEstimate(pendingEstimate.Id);

        workOrder.ApproveEstimate(approvedEstimate.Id);
        workOrder.StartWork();
        workOrder.Complete();
        var statusChangedEventsBefore = workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count();
        var rejectedEventsBefore = workOrder.DomainEvents.OfType<EstimateRejected>().Count();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.RejectEstimate(pendingEstimate.Id));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(EstimateStatus.Pending, pendingEstimate.Status);
        Assert.Equal(statusChangedEventsBefore, workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count());
        Assert.Equal(rejectedEventsBefore, workOrder.DomainEvents.OfType<EstimateRejected>().Count());
    }

    [Fact]
    public void ApproveEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsDelivered_AndKeepPendingEstimateUnchanged()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var approvedEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, approvedEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, approvedEstimate.Id);
        workOrder.SubmitEstimate(approvedEstimate.Id);

        var pendingEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, pendingEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, pendingEstimate.Id);
        workOrder.SubmitEstimate(pendingEstimate.Id);

        workOrder.ApproveEstimate(approvedEstimate.Id);
        workOrder.StartWork();
        workOrder.Complete();
        workOrder.Deliver();
        var statusChangedEventsBefore = workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count();
        var approvedEventsBefore = workOrder.DomainEvents.OfType<EstimateApproved>().Count();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.ApproveEstimate(pendingEstimate.Id));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
        Assert.Equal(WorkOrderStatus.Delivered, workOrder.Status);
        Assert.Equal(EstimateStatus.Pending, pendingEstimate.Status);
        Assert.Equal(statusChangedEventsBefore, workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count());
        Assert.Equal(approvedEventsBefore, workOrder.DomainEvents.OfType<EstimateApproved>().Count());
    }

    [Fact]
    public void RejectEstimate_ShouldThrowBusinessRuleViolationException_WhenWorkOrderIsDelivered_AndKeepPendingEstimateUnchanged()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var approvedEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, approvedEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, approvedEstimate.Id);
        workOrder.SubmitEstimate(approvedEstimate.Id);

        var pendingEstimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, pendingEstimate.Id);
        WorkOrderBuilder.AddDefaultServiceLine(workOrder, pendingEstimate.Id);
        workOrder.SubmitEstimate(pendingEstimate.Id);

        workOrder.ApproveEstimate(approvedEstimate.Id);
        workOrder.StartWork();
        workOrder.Complete();
        workOrder.Deliver();
        var statusChangedEventsBefore = workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count();
        var rejectedEventsBefore = workOrder.DomainEvents.OfType<EstimateRejected>().Count();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.RejectEstimate(pendingEstimate.Id));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
        Assert.Equal(WorkOrderStatus.Delivered, workOrder.Status);
        Assert.Equal(EstimateStatus.Pending, pendingEstimate.Status);
        Assert.Equal(statusChangedEventsBefore, workOrder.DomainEvents.OfType<WorkOrderStatusChanged>().Count());
        Assert.Equal(rejectedEventsBefore, workOrder.DomainEvents.OfType<EstimateRejected>().Count());
    }

    [Fact]
    public void Estimate_MutatorMethods_ShouldNotBePublic()
    {
        var publicInstanceMethods = typeof(Estimate)
            .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("AddInventoryLine", publicInstanceMethods);
        Assert.DoesNotContain("AddServiceLine", publicInstanceMethods);
        Assert.DoesNotContain("Submit", publicInstanceMethods);
        Assert.DoesNotContain("Approve", publicInstanceMethods);
        Assert.DoesNotContain("Reject", publicInstanceMethods);
        Assert.DoesNotContain("Cancel", publicInstanceMethods);
    }

    [Fact]
    public void AddServiceLine_ShouldCreatePendingServiceLine()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();

        workOrder.AddServiceLine(
            estimate.Id,
            ServiceId.New(),
            Description.Create("Timing service"),
            Price.Create(80m));

        var serviceLine = Assert.Single(estimate.ServiceLines);
        Assert.Equal(EstimateServiceLineStatus.Pending, serviceLine.Status);
        Assert.Null(serviceLine.StartedAt);
        Assert.Null(serviceLine.CompletedAt);
    }

    [Fact]
    public void StartEstimateService_ShouldMovePendingServiceToInProgress_AndStartWorkOrder()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimate = workOrder.Estimates.Single();
        var serviceLine = estimate.ServiceLines.Single();
        var beforeStart = DateTime.UtcNow;

        workOrder.StartEstimateService(estimate.Id, serviceLine.Id);

        var afterStart = DateTime.UtcNow;
        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        Assert.Equal(EstimateServiceLineStatus.InProgress, serviceLine.Status);
        Assert.NotNull(workOrder.StartedAt);
        Assert.NotNull(serviceLine.StartedAt);
        Assert.InRange(serviceLine.StartedAt.Value, beforeStart, afterStart);
        Assert.Equal(workOrder.StartedAt, serviceLine.StartedAt);
    }

    [Fact]
    public void CompleteEstimateService_ShouldCompleteService_AndCompleteWorkOrder_WhenAllServicesAreCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimate = workOrder.Estimates.Single();
        var serviceLine = estimate.ServiceLines.Single();
        workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
        var beforeComplete = DateTime.UtcNow;

        workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id);

        var afterComplete = DateTime.UtcNow;
        Assert.Equal(EstimateServiceLineStatus.Completed, serviceLine.Status);
        Assert.NotNull(serviceLine.CompletedAt);
        Assert.InRange(serviceLine.CompletedAt.Value, beforeComplete, afterComplete);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(serviceLine.CompletedAt, workOrder.CompletedAt);
    }

    [Fact]
    public void CompleteEstimateService_ShouldKeepWorkOrderInProgress_WhenAnotherServiceIsNotCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        workOrder.AddServiceLine(estimate.Id, ServiceId.New(), Description.Create("First service"), Price.Create(100m));
        workOrder.AddServiceLine(estimate.Id, ServiceId.New(), Description.Create("Second service"), Price.Create(150m));
        workOrder.SubmitEstimate(estimate.Id);
        workOrder.ApproveEstimate(estimate.Id);
        var firstService = estimate.ServiceLines.First();

        workOrder.StartEstimateService(estimate.Id, firstService.Id);
        workOrder.CompleteEstimateService(estimate.Id, firstService.Id);

        Assert.Equal(WorkOrderStatus.InProgress, workOrder.Status);
        Assert.Equal(EstimateServiceLineStatus.Completed, firstService.Status);
        Assert.Contains(estimate.ServiceLines, line => line.Status == EstimateServiceLineStatus.Pending);
        Assert.Null(workOrder.CompletedAt);
    }

    [Fact]
    public void CompleteEstimateService_ShouldThrowBusinessRuleViolationException_WhenServiceIsPending()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimate = workOrder.Estimates.Single();
        var serviceLine = estimate.ServiceLines.Single();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id));

        Assert.Equal("Work order must be in progress before completing services.", exception.Message);
        Assert.Equal(EstimateServiceLineStatus.Pending, serviceLine.Status);
        Assert.Null(serviceLine.CompletedAt);
    }

    [Fact]
    public void StartEstimateService_ShouldThrowBusinessRuleViolationException_WhenServiceIsCompleted()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimate = workOrder.Estimates.Single();
        var serviceLine = estimate.ServiceLines.Single();
        workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
        workOrder.CompleteEstimateService(estimate.Id, serviceLine.Id);

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => workOrder.StartEstimateService(estimate.Id, serviceLine.Id));

        Assert.Equal("Finalized work orders cannot be changed.", exception.Message);
        Assert.Equal(EstimateServiceLineStatus.Completed, serviceLine.Status);
    }

    [Fact]
    public void SubmitEstimate_ShouldThrowBusinessRuleViolationException_WhenEstimateHasOnlyInventory()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        WorkOrderBuilder.AddDefaultInventoryLine(workOrder, estimate.Id);

        var exception = Assert.Throws<BusinessRuleViolationException>(() => workOrder.SubmitEstimate(estimate.Id));

        Assert.Equal("Estimate must contain at least one service line before submission.", exception.Message);
        Assert.Equal(WorkOrderStatus.Created, workOrder.Status);
        Assert.Equal(EstimateStatus.Draft, estimate.Status);
    }

    [Fact]
    public void StartEstimateService_CalledTwice_ShouldThrowBusinessRuleViolationException_AndKeepOriginalStartedAt()
    {
        var workOrder = new WorkOrderBuilder().BuildWithApprovedEstimate();
        var estimate = workOrder.Estimates.Single();
        var serviceLine = estimate.ServiceLines.Single();
        workOrder.StartEstimateService(estimate.Id, serviceLine.Id);
        var originalStartedAt = serviceLine.StartedAt;

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => workOrder.StartEstimateService(estimate.Id, serviceLine.Id));

        Assert.Equal("Estimate service line status transition from 'InProgress' to 'InProgress' is not allowed.", exception.Message);
        Assert.Equal(EstimateServiceLineStatus.InProgress, serviceLine.Status);
        Assert.Equal(originalStartedAt, serviceLine.StartedAt);
        Assert.Null(serviceLine.CompletedAt);
    }

    [Fact]
    public void CompleteEstimateService_CalledTwiceForSameLine_ShouldThrowBusinessRuleViolationException_AndKeepOriginalCompletedAt()
    {
        var workOrder = new WorkOrderBuilder().BuildCreated();
        var estimate = workOrder.CreateEstimate();
        workOrder.AddServiceLine(estimate.Id, ServiceId.New(), Description.Create("First service"), Price.Create(100m));
        workOrder.AddServiceLine(estimate.Id, ServiceId.New(), Description.Create("Second service"), Price.Create(150m));
        workOrder.SubmitEstimate(estimate.Id);
        workOrder.ApproveEstimate(estimate.Id);

        var firstService = estimate.ServiceLines.First();
        workOrder.StartEstimateService(estimate.Id, firstService.Id);
        workOrder.CompleteEstimateService(estimate.Id, firstService.Id);
        var originalCompletedAt = firstService.CompletedAt;

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => workOrder.CompleteEstimateService(estimate.Id, firstService.Id));

        Assert.Equal("Estimate service line status transition from 'Completed' to 'Completed' is not allowed.", exception.Message);
        Assert.Equal(EstimateServiceLineStatus.Completed, firstService.Status);
        Assert.Equal(originalCompletedAt, firstService.CompletedAt);
    }
}
