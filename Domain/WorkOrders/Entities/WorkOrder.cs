using GarageFlow.SharedKernel.Domain.Entities;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Domain.Interfaces;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.Vehicles.ValueObjects;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.Events;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Entities;

public sealed class WorkOrder : Entity<WorkOrderId>, IAggregateRoot
{
    private readonly List<Estimate> _estimates = [];

    public CustomerId CustomerId { get; private set; }
    public VehicleId VehicleId { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public Guid? ExecutionId { get; private set; }
    public string? CheckoutUrl { get; private set; }
    public long BillingPaymentRevision { get; private set; }
    public DateTime? PaymentConfirmedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public IReadOnlyCollection<Estimate> Estimates => _estimates.AsReadOnly();

    private WorkOrder(
        WorkOrderId id,
        CustomerId customerId,
        VehicleId vehicleId) : base(id)
    {
        CustomerId = EnsureValidCustomerId(customerId);
        VehicleId = EnsureValidVehicleId(vehicleId);
        Status = WorkOrderStatus.Received;
    }

    public static WorkOrder Create(CustomerId customerId, VehicleId vehicleId)
    {
        var id = WorkOrderId.New();
        var workOrder = new WorkOrder(id, customerId, vehicleId);

        workOrder.RaiseDomainEvent(new WorkOrderCreated(
            WorkOrderId: id,
            CustomerId: workOrder.CustomerId,
            VehicleId: workOrder.VehicleId,
            Status: workOrder.Status,
            CreatedAt: workOrder.CreatedAt));

        return workOrder;
    }

    public Estimate CreateEstimate()
    {
        EnsureWorkOrderAllowsEstimateContentChanges();

        var estimate = Estimate.Create(Id);
        _estimates.Add(estimate);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new EstimateCreated(
            WorkOrderId: Id,
            EstimateId: estimate.Id,
            Status: estimate.Status,
            CreatedAt: estimate.CreatedAt));

        return estimate;
    }

    public void EnsureEstimateCanBeEdited(EstimateId estimateId)
    {
        _ = GetEditableEstimateOrThrow(estimateId);
    }

    public void AddInventoryLine(
        EstimateId estimateId,
        InventoryItemId inventoryItemId,
        Description description,
        EstimateItemQuantity quantity,
        Price unitCost,
        Price unitPrice)
    {
        var estimate = GetEditableEstimateOrThrow(estimateId);
        var line = estimate.AddInventoryLine(
            inventoryItemId,
            description,
            quantity,
            unitCost,
            unitPrice);

        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new EstimateInventoryLineAdded(
            WorkOrderId: Id,
            EstimateId: estimate.Id,
            EstimateInventoryLineId: line.Id,
            InventoryItemId: line.InventoryItemId,
            Quantity: line.Quantity.Value,
            UnitCost: line.UnitCost.Value,
            UnitPrice: line.UnitPrice.Value,
            TotalPrice: line.TotalPrice.Value));
    }

    public void AddServiceLine(
        EstimateId estimateId,
        ServiceId serviceId,
        Description description,
        Price unitPrice)
    {
        var estimate = GetEditableEstimateOrThrow(estimateId);
        var line = estimate.AddServiceLine(serviceId, description, unitPrice);

        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new EstimateServiceLineAdded(
            WorkOrderId: Id,
            EstimateId: estimate.Id,
            EstimateServiceLineId: line.Id,
            ServiceId: line.ServiceId,
            UnitPrice: line.UnitPrice.Value,
            TotalPrice: line.TotalPrice.Value));
    }

    public void SubmitEstimate(EstimateId estimateId)
    {
        var estimate = GetEstimateOrThrow(estimateId);
        EnsureStatusAllowsEstimateSubmission();
        EnsureEstimateCanBeSubmitted(estimate);
        var previousStatus = Status;
        AdvanceStatusForEstimateSubmission();
        estimate.Submit();

        if (previousStatus != WorkOrderStatus.WaitingApproval && Status == WorkOrderStatus.WaitingApproval)
        {
            RaiseDomainEvent(new EstimateWaitingApprovalRequested(
                WorkOrderId: Id,
                EstimateId: estimate.Id,
                CustomerId: CustomerId,
                RequestedAt: DateTime.UtcNow));
        }

        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new EstimateSubmitted(
            WorkOrderId: Id,
            EstimateId: estimate.Id,
            Status: estimate.Status,
            TotalAmount: estimate.TotalAmount.Value,
            SubmittedAt: DateTime.UtcNow));
    }

    public void ApproveEstimate(EstimateId estimateId, bool scheduleExecution = false)
    {
        EnsureNotFinalizedForContentChanges();

        var estimate = GetEstimateOrThrow(estimateId);
        if (estimate.Status != EstimateStatus.Pending)
        {
            throw new BusinessRuleViolationException("Only pending estimates can be approved.");
        }

        if (_estimates.Any(other => other.Id != estimateId && other.Status == EstimateStatus.Approved))
        {
            throw new BusinessRuleViolationException("A work order cannot have more than one approved estimate.");
        }

        if (Status != WorkOrderStatus.WaitingApproval)
        {
            throw new BusinessRuleViolationException("Only work orders waiting for approval can have an estimate approved.");
        }

        if (scheduleExecution && (estimate.ServiceLines.Count is < 1 or > ExecutionSchedulingRequested.MaximumServiceLines))
        {
            throw new BusinessRuleViolationException("The approved estimate exceeds the supported execution scheduling limits.");
        }

        var approvedAt = DateTime.UtcNow;
        estimate.Approve();
        if (scheduleExecution)
        {
            var executionId = Guid.NewGuid();
            ExecutionId = executionId;
            TransitionTo(WorkOrderStatus.WaitingExecutionSchedule, approvedAt);
            RaiseDomainEvent(new ExecutionSchedulingRequested(
                Id, estimateId, executionId, estimate.ServiceLines.Select(line => line.Id.Value).ToArray(), approvedAt));
        }
        else
        {
            TransitionTo(WorkOrderStatus.InProgress, approvedAt);
            StartedAt = approvedAt;
        }

        UpdatedAt = approvedAt;

        RaiseDomainEvent(new EstimateApproved(
            WorkOrderId: Id,
            EstimateId: estimate.Id,
            Status: estimate.Status,
            ApprovedAt: approvedAt));
    }

    public IReadOnlyList<InventoryReservation> ApplyExecutionScheduleDecision(
        EstimateId estimateId, Guid executionId, bool accepted)
    {
        _ = GetMatchingExecutionEstimate(estimateId, executionId);
        var resultingStatus = accepted ? WorkOrderStatus.ExecutionScheduled : WorkOrderStatus.ExecutionScheduleRejected;
        if (Status == resultingStatus || (accepted && Status is (
            WorkOrderStatus.ExecutionStartRequested or WorkOrderStatus.InProgress or
            WorkOrderStatus.ExecutionCompletionRequested or WorkOrderStatus.Completed or WorkOrderStatus.Delivered)))
        {
            return [];
        }

        if (Status != WorkOrderStatus.WaitingExecutionSchedule)
        {
            throw new BusinessRuleViolationException("The work order is not waiting for an execution scheduling decision.");
        }

        var reservations = accepted ? [] : GetActiveReservations();
        TransitionTo(resultingStatus);
        return reservations;
    }

    public void RequestExecutionStart()
    {
        var executionId = GetDistributedExecutionId();
        if (Status is WorkOrderStatus.ExecutionStartRequested or WorkOrderStatus.InProgress or
            WorkOrderStatus.ExecutionCompletionRequested or WorkOrderStatus.Completed or WorkOrderStatus.Delivered)
        {
            return;
        }

        RequestExecutionTransition(executionId, WorkOrderStatus.ExecutionStartRequested, complete: false);
    }

    public void RequestExecutionCompletion()
    {
        var executionId = GetDistributedExecutionId();
        if (Status is WorkOrderStatus.ExecutionCompletionRequested or WorkOrderStatus.Completed or WorkOrderStatus.Delivered)
        {
            return;
        }

        RequestExecutionTransition(executionId, WorkOrderStatus.ExecutionCompletionRequested, complete: true);
    }

    public void ApplyExecutionStarted(EstimateId estimateId, Guid executionId, DateTime startedAt)
    {
        var estimate = GetMatchingExecutionEstimate(estimateId, executionId);
        if (Status is WorkOrderStatus.InProgress or WorkOrderStatus.ExecutionCompletionRequested or
            WorkOrderStatus.Completed or WorkOrderStatus.Delivered)
        {
            return;
        }

        if (Status != WorkOrderStatus.ExecutionStartRequested || startedAt.Kind != DateTimeKind.Utc)
        {
            throw new BusinessRuleViolationException("Execution start does not match a pending start request.");
        }

        foreach (var line in estimate.ServiceLines)
        {
            line.Start(startedAt);
        }

        StartedAt = startedAt;
        TransitionTo(WorkOrderStatus.InProgress, startedAt);
    }

    public void ApplyExecutionCompleted(EstimateId estimateId, Guid executionId, DateTime completedAt)
    {
        var estimate = GetMatchingExecutionEstimate(estimateId, executionId);
        if (Status is WorkOrderStatus.Completed or WorkOrderStatus.Delivered)
        {
            return;
        }

        if (Status != WorkOrderStatus.ExecutionCompletionRequested || completedAt.Kind != DateTimeKind.Utc ||
            StartedAt is not { } startedAt || completedAt < startedAt)
        {
            throw new BusinessRuleViolationException("Execution completion does not match a pending completion request.");
        }

        var amount = estimate.TotalAmount.Value;
        foreach (var line in estimate.ServiceLines)
        {
            line.Complete(completedAt);
        }

        CompleteFromServiceLines(completedAt);
        RaiseDomainEvent(new ExecutionChargeRequested(
            Id, estimateId, executionId, Guid.NewGuid(), amount, completedAt));
    }

    private void RequestExecutionTransition(Guid executionId, WorkOrderStatus status, bool complete)
    {
        var estimate = GetSingleApprovedEstimateOrThrow();
        var requestedAt = DateTime.UtcNow;
        TransitionTo(status, requestedAt);
        RaiseDomainEvent(new ExecutionLifecycleRequested(
            Id, estimate.Id, executionId, Guid.NewGuid(), complete, requestedAt));
    }

    private Guid GetDistributedExecutionId() => ExecutionId ??
        throw new BusinessRuleViolationException("The work order has no distributed execution.");

    private Estimate GetMatchingExecutionEstimate(EstimateId estimateId, Guid executionId)
    {
        var estimate = GetSingleApprovedEstimateOrThrow();
        if (ExecutionId != executionId || estimate.Id != estimateId)
        {
            throw new BusinessRuleViolationException("The execution result does not match the approved schedule.");
        }

        return estimate;
    }

    private void EnsureLocalExecution()
    {
        if (ExecutionId.HasValue)
        {
            throw new BusinessRuleViolationException("The execution service owns repair progress for this work order.");
        }
    }

    public IReadOnlyList<InventoryReservation> RejectEstimate(EstimateId estimateId)
    {
        EnsureNotFinalizedForContentChanges();

        var estimate = GetEstimateOrThrow(estimateId);
        if (estimate.Status != EstimateStatus.Pending)
        {
            throw new BusinessRuleViolationException("Only pending estimates can be rejected.");
        }

        if (Status != WorkOrderStatus.WaitingApproval)
        {
            throw new BusinessRuleViolationException("Only work orders waiting for approval can have an estimate rejected.");
        }

        estimate.Reject();
        var rejectedAt = DateTime.UtcNow;
        TransitionTo(WorkOrderStatus.Diagnosing, rejectedAt);

        RaiseDomainEvent(new EstimateRejected(
            WorkOrderId: Id,
            EstimateId: estimate.Id,
            Status: estimate.Status,
            RejectedAt: rejectedAt));

        return GroupInventoryReservations([estimate]);
    }

    public void StartDiagnosis()
    {
        if (Status != WorkOrderStatus.Received)
        {
            throw new BusinessRuleViolationException("Only received work orders can start diagnosis.");
        }

        TransitionTo(WorkOrderStatus.Diagnosing);
    }

    public void StartEstimateService(EstimateId estimateId, EstimateServiceLineId lineId)
    {
        EnsureLocalExecution();

        EnsureNotFinalizedForContentChanges();

        var estimate = GetApprovedEstimateOrThrow(estimateId);
        var serviceLine = GetEstimateServiceLineOrThrow(estimate, lineId);
        if (Status != WorkOrderStatus.InProgress)
        {
            throw new BusinessRuleViolationException("Work order must be approved before starting services.");
        }

        serviceLine.Start();

        RaiseDomainEvent(new EstimateServiceLineStarted(
            WorkOrderId: Id,
            EstimateId: estimate.Id,
            EstimateServiceLineId: serviceLine.Id,
            ServiceId: serviceLine.ServiceId,
            Status: serviceLine.Status,
            StartedAt: serviceLine.StartedAt!.Value));

        UpdatedAt = DateTime.UtcNow;
    }

    public void CompleteEstimateService(EstimateId estimateId, EstimateServiceLineId lineId)
    {
        EnsureLocalExecution();

        if (Status != WorkOrderStatus.InProgress)
        {
            throw new BusinessRuleViolationException("Work order must be in progress before completing services.");
        }

        var estimate = GetApprovedEstimateOrThrow(estimateId);
        var serviceLine = GetEstimateServiceLineOrThrow(estimate, lineId);

        serviceLine.Complete();

        RaiseDomainEvent(new EstimateServiceLineCompleted(
            WorkOrderId: Id,
            EstimateId: estimate.Id,
            EstimateServiceLineId: serviceLine.Id,
            ServiceId: serviceLine.ServiceId,
            Status: serviceLine.Status,
            CompletedAt: serviceLine.CompletedAt!.Value));

        if (estimate.ServiceLines.All(line => line.Status == EstimateServiceLineStatus.Completed))
        {
            CompleteFromServiceLines(serviceLine.CompletedAt);
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        EnsureLocalExecution();

        CompleteFromServiceLines(null);
    }

    private void CompleteFromServiceLines(DateTime? completedAt)
    {
        var approvedEstimate = GetSingleApprovedEstimateOrThrow();
        if (approvedEstimate.ServiceLines.Count == 0 ||
            approvedEstimate.ServiceLines.Any(line => line.Status != EstimateServiceLineStatus.Completed))
        {
            throw new BusinessRuleViolationException("All approved estimate service lines must be completed before completion.");
        }

        if (Status == WorkOrderStatus.Completed)
        {
            return;
        }

        var occurredAt = completedAt ?? DateTime.UtcNow;
        TransitionTo(WorkOrderStatus.Completed, occurredAt);
        CompletedAt = occurredAt;
    }

    public void ApplyBillingCheckout(EstimateId estimateId, Guid chargeId, decimal amountInMinorUnits, string checkoutUrl)
    {
        EnsureMatchingCharge(estimateId, chargeId, amountInMinorUnits);
        if (amountInMinorUnits == 0 || !IsMercadoPagoCheckout(checkoutUrl))
        {
            throw new BusinessRuleViolationException("The checkout is invalid for this charge.");
        }

        if (string.Equals(CheckoutUrl, checkoutUrl, StringComparison.Ordinal))
        {
            return;
        }

        if (CheckoutUrl is not null)
        {
            throw new BusinessRuleViolationException("The charge already has a different checkout.");
        }

        CheckoutUrl = checkoutUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyBillingPayment(
        EstimateId estimateId,
        Guid chargeId,
        decimal amountInMinorUnits,
        long revision,
        WorkOrderPaymentState state,
        DateTime occurredAt)
    {
        EnsureMatchingCharge(estimateId, chargeId, amountInMinorUnits);
        if (revision <= 0 || occurredAt.Kind != DateTimeKind.Utc || !IsPaymentStateValid(state, amountInMinorUnits))
        {
            throw new BusinessRuleViolationException("The payment state is invalid for this charge.");
        }

        if (revision <= BillingPaymentRevision)
        {
            return;
        }

        BillingPaymentRevision = revision;
        PaymentConfirmedAt = state == WorkOrderPaymentState.Revoked ? null : occurredAt;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureMatchingCharge(EstimateId estimateId, Guid chargeId, decimal amountInMinorUnits)
    {
        var estimate = GetMatchingExecutionEstimate(estimateId, chargeId);
        if (Status is not (WorkOrderStatus.Completed or WorkOrderStatus.Delivered) ||
            estimate.TotalAmount.Value * 100m != amountInMinorUnits)
        {
            throw new BusinessRuleViolationException("The billing result does not match the completed work order.");
        }
    }

    private static bool IsPaymentStateValid(WorkOrderPaymentState state, decimal amountInMinorUnits) =>
        state switch
        {
            WorkOrderPaymentState.Paid or WorkOrderPaymentState.Revoked => amountInMinorUnits > 0,
            WorkOrderPaymentState.NoPaymentRequired => amountInMinorUnits == 0,
            _ => false
        };

    private static bool IsMercadoPagoCheckout(string value) =>
        value is { Length: <= 2048 } &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.Host is "www.mercadopago.com.br" or "sandbox.mercadopago.com.br") &&
        uri.AbsolutePath.StartsWith("/checkout/", StringComparison.Ordinal);

    public void Deliver()
    {
        if (Status != WorkOrderStatus.Completed)
        {
            throw new BusinessRuleViolationException("Only completed work orders can be delivered.");
        }

        if (ExecutionId.HasValue && PaymentConfirmedAt is null)
        {
            throw new BusinessRuleViolationException("Payment confirmation is required before delivery.");
        }

        TransitionTo(WorkOrderStatus.Delivered);
    }

    public IReadOnlyList<InventoryReservation> Cancel()
    {
        if (Status is WorkOrderStatus.Cancelled or WorkOrderStatus.ExecutionScheduleRejected)
        {
            return [];
        }

        EnsureLocalExecution();
        var reservations = GetActiveReservations();
        TransitionTo(WorkOrderStatus.Cancelled);
        return reservations;
    }

    private List<InventoryReservation> GetActiveReservations() =>
        GroupInventoryReservations(_estimates.Where(estimate =>
            estimate.Status is EstimateStatus.Draft or EstimateStatus.Pending or EstimateStatus.Approved));

    private static List<InventoryReservation> GroupInventoryReservations(IEnumerable<Estimate> estimates)
    {
        return estimates
            .SelectMany(estimate => estimate.InventoryLines)
            .GroupBy(line => line.InventoryItemId)
            .Select(group => new InventoryReservation(
                group.Key,
                EstimateItemQuantity.Create(group.Sum(line => line.Quantity.Value))))
            .OrderBy(reservation => reservation.InventoryItemId.Value)
            .ToList();
    }

    private Estimate GetEstimateOrThrow(EstimateId estimateId)
    {
        var estimate = _estimates.SingleOrDefault(candidate => candidate.Id == estimateId);
        if (estimate is null)
        {
            throw new NotFoundException($"Estimate with ID '{estimateId.Value}' was not found.");
        }

        return estimate;
    }

    private Estimate GetApprovedEstimateOrThrow(EstimateId estimateId)
    {
        var estimate = GetEstimateOrThrow(estimateId);
        if (estimate.Status != EstimateStatus.Approved)
        {
            throw new BusinessRuleViolationException("Only approved estimates can have services executed.");
        }

        return estimate;
    }

    private Estimate GetSingleApprovedEstimateOrThrow()
    {
        var approvedEstimates = _estimates.Where(estimate => estimate.Status == EstimateStatus.Approved).ToList();
        if (approvedEstimates.Count != 1)
        {
            throw new BusinessRuleViolationException("Work order requires exactly one approved estimate before completion.");
        }

        return approvedEstimates[0];
    }

    private static EstimateServiceLine GetEstimateServiceLineOrThrow(Estimate estimate, EstimateServiceLineId lineId)
    {
        var serviceLine = estimate.ServiceLines.SingleOrDefault(candidate => candidate.Id == lineId);
        if (serviceLine is null)
        {
            throw new NotFoundException($"Estimate service line with ID '{lineId.Value}' was not found.");
        }

        return serviceLine;
    }

    private Estimate GetEditableEstimateOrThrow(EstimateId estimateId)
    {
        EnsureWorkOrderAllowsEstimateContentChanges();

        var estimate = GetEstimateOrThrow(estimateId);
        if (estimate.Status != EstimateStatus.Draft)
        {
            throw new BusinessRuleViolationException("Only draft estimates can be edited.");
        }

        return estimate;
    }

    private void EnsureWorkOrderAllowsEstimateContentChanges()
    {
        if (Status is WorkOrderStatus.Completed or WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled)
        {
            throw new BusinessRuleViolationException("Finalized work orders cannot be changed.");
        }

        if (Status == WorkOrderStatus.InProgress)
        {
            throw new BusinessRuleViolationException("In-progress work orders cannot be changed.");
        }

        if (ExecutionId.HasValue)
        {
            throw new BusinessRuleViolationException("Work orders with an approved execution cannot be changed.");
        }
    }

    private void EnsureNotFinalizedForContentChanges()
    {
        if (Status is WorkOrderStatus.Completed or WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled)
        {
            throw new BusinessRuleViolationException("Finalized work orders cannot be changed.");
        }
    }

    private void EnsureStatusAllowsEstimateSubmission()
    {
        if (Status is WorkOrderStatus.Completed or WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled)
        {
            throw new BusinessRuleViolationException("Finalized work orders cannot be changed.");
        }

        if (Status is WorkOrderStatus.Received or WorkOrderStatus.Diagnosing or WorkOrderStatus.WaitingApproval)
        {
            return;
        }

        throw new BusinessRuleViolationException($"Work order status '{Status}' does not allow estimate submission.");
    }

    private static void EnsureEstimateCanBeSubmitted(Estimate estimate)
    {
        if (estimate.Status != EstimateStatus.Draft)
        {
            throw new BusinessRuleViolationException("Only draft estimates can be edited.");
        }

        if (estimate.ServiceLines.Count == 0)
        {
            throw new BusinessRuleViolationException("Estimate must contain at least one service line before submission.");
        }
    }

    private void AdvanceStatusForEstimateSubmission()
    {
        if (Status == WorkOrderStatus.Received)
        {
            TransitionTo(WorkOrderStatus.Diagnosing);
            TransitionTo(WorkOrderStatus.WaitingApproval);
            return;
        }

        if (Status == WorkOrderStatus.Diagnosing)
        {
            TransitionTo(WorkOrderStatus.WaitingApproval);
        }
    }

    private void TransitionTo(WorkOrderStatus newStatus, DateTime? occurredAt = null)
    {
        if (Status == newStatus)
        {
            return;
        }

        var isAllowed = (Status, newStatus) switch
        {
            (WorkOrderStatus.Received, WorkOrderStatus.Diagnosing) => true,
            (WorkOrderStatus.Diagnosing, WorkOrderStatus.WaitingApproval) => true,
            (WorkOrderStatus.WaitingApproval, WorkOrderStatus.InProgress) => true,
            (WorkOrderStatus.WaitingApproval, WorkOrderStatus.WaitingExecutionSchedule) => true,
            (WorkOrderStatus.WaitingExecutionSchedule, WorkOrderStatus.ExecutionScheduled) => true,
            (WorkOrderStatus.WaitingExecutionSchedule, WorkOrderStatus.ExecutionScheduleRejected) => true,
            (WorkOrderStatus.WaitingApproval, WorkOrderStatus.Diagnosing) => true,
            (WorkOrderStatus.InProgress, WorkOrderStatus.Completed) => true,
            (WorkOrderStatus.ExecutionScheduled, WorkOrderStatus.ExecutionStartRequested) => true,
            (WorkOrderStatus.ExecutionStartRequested, WorkOrderStatus.InProgress) => true,
            (WorkOrderStatus.InProgress, WorkOrderStatus.ExecutionCompletionRequested) => true,
            (WorkOrderStatus.ExecutionCompletionRequested, WorkOrderStatus.Completed) => true,
            (WorkOrderStatus.Completed, WorkOrderStatus.Delivered) => true,
            (WorkOrderStatus.Received, WorkOrderStatus.Cancelled) => true,
            (WorkOrderStatus.Diagnosing, WorkOrderStatus.Cancelled) => true,
            (WorkOrderStatus.WaitingApproval, WorkOrderStatus.Cancelled) => true,
            (WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled) => true,
            _ => false
        };

        if (!isAllowed)
        {
            throw new BusinessRuleViolationException($"Work order status transition from '{Status}' to '{newStatus}' is not allowed.");
        }

        var previousStatus = Status;
        Status = newStatus;
        UpdatedAt = occurredAt ?? DateTime.UtcNow;

        RaiseDomainEvent(new WorkOrderStatusChanged(
            WorkOrderId: Id,
            PreviousStatus: previousStatus,
            NewStatus: newStatus,
            UpdatedAt: UpdatedAt));
    }

    private static CustomerId EnsureValidCustomerId(CustomerId customerId)
    {
        if (customerId.Value == Guid.Empty)
        {
            throw new ValidationException("Customer identifier cannot be empty.");
        }

        return customerId;
    }

    private static VehicleId EnsureValidVehicleId(VehicleId vehicleId)
    {
        if (vehicleId.Value == Guid.Empty)
        {
            throw new ValidationException("Vehicle identifier cannot be empty.");
        }

        return vehicleId;
    }
}
