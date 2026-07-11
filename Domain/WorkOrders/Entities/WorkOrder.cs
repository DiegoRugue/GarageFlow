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
        Status = WorkOrderStatus.Created;
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

    public void ApproveEstimate(EstimateId estimateId)
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

        estimate.Approve();
        if (Status == WorkOrderStatus.WaitingApproval)
        {
            TransitionTo(WorkOrderStatus.Approved);
        }

        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new EstimateApproved(
            WorkOrderId: Id,
            EstimateId: estimate.Id,
            Status: estimate.Status,
            ApprovedAt: DateTime.UtcNow));
    }

    public Estimate RejectEstimate(EstimateId estimateId)
    {
        EnsureNotFinalizedForContentChanges();

        var estimate = GetEstimateOrThrow(estimateId);
        estimate.Reject();

        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new EstimateRejected(
            WorkOrderId: Id,
            EstimateId: estimate.Id,
            Status: estimate.Status,
            RejectedAt: DateTime.UtcNow));

        return estimate;
    }

    public void StartDiagnosis()
    {
        TransitionTo(WorkOrderStatus.Diagnosing);
    }

    public void StartWork()
    {
        if (Status == WorkOrderStatus.WaitingApproval)
        {
            throw new BusinessRuleViolationException("Work order cannot start while waiting for customer approval.");
        }

        if (Status == WorkOrderStatus.InProgress)
        {
            return;
        }

        var startedAt = DateTime.UtcNow;
        TransitionTo(WorkOrderStatus.InProgress, startedAt);
        StartedAt = startedAt;
    }

    public void StartEstimateService(EstimateId estimateId, EstimateServiceLineId lineId)
    {
        EnsureNotFinalizedForContentChanges();

        var estimate = GetApprovedEstimateOrThrow(estimateId);
        var serviceLine = GetEstimateServiceLineOrThrow(estimate, lineId);
        var startedAtBeforeLineStart = StartedAt;

        if (Status == WorkOrderStatus.Approved)
        {
            StartWork();
        }
        else if (Status != WorkOrderStatus.InProgress)
        {
            throw new BusinessRuleViolationException("Work order must be approved before starting services.");
        }

        serviceLine.Start();

        if (startedAtBeforeLineStart is null)
        {
            StartedAt = serviceLine.StartedAt;
        }

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

    public void Deliver()
    {
        if (Status != WorkOrderStatus.Completed)
        {
            throw new BusinessRuleViolationException("Only completed work orders can be delivered.");
        }

        TransitionTo(WorkOrderStatus.Delivered);
    }

    public void Cancel()
    {
        TransitionTo(WorkOrderStatus.Cancelled);
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

        if (Status is WorkOrderStatus.Approved or WorkOrderStatus.InProgress)
        {
            throw new BusinessRuleViolationException("Approved or in-progress work orders cannot be changed.");
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

        if (Status is WorkOrderStatus.Created or WorkOrderStatus.Diagnosing or WorkOrderStatus.WaitingApproval)
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
        if (Status == WorkOrderStatus.Created)
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
            (WorkOrderStatus.Created, WorkOrderStatus.Diagnosing) => true,
            (WorkOrderStatus.Diagnosing, WorkOrderStatus.WaitingApproval) => true,
            (WorkOrderStatus.WaitingApproval, WorkOrderStatus.Approved) => true,
            (WorkOrderStatus.Approved, WorkOrderStatus.InProgress) => true,
            (WorkOrderStatus.InProgress, WorkOrderStatus.Completed) => true,
            (WorkOrderStatus.Completed, WorkOrderStatus.Delivered) => true,
            (WorkOrderStatus.Created, WorkOrderStatus.Cancelled) => true,
            (WorkOrderStatus.Diagnosing, WorkOrderStatus.Cancelled) => true,
            (WorkOrderStatus.WaitingApproval, WorkOrderStatus.Cancelled) => true,
            (WorkOrderStatus.Approved, WorkOrderStatus.Cancelled) => true,
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
