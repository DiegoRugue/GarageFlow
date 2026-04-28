using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.Interfaces;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
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
        EnsureNotFinalizedForContentChanges();

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

    public void AddInventoryLine(
        EstimateId estimateId,
        InventoryItemId inventoryItemId,
        Description description,
        EstimateItemQuantity quantity,
        Price unitCost,
        Price unitPrice)
    {
        EnsureNotFinalizedForContentChanges();

        var estimate = GetEstimateOrThrow(estimateId);
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
        EnsureNotFinalizedForContentChanges();

        var estimate = GetEstimateOrThrow(estimateId);
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
        AdvanceStatusForEstimateSubmission();
        estimate.Submit();

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

        TransitionTo(WorkOrderStatus.InProgress);
    }

    public void Complete()
    {
        var approvedEstimatesCount = _estimates.Count(estimate => estimate.Status == EstimateStatus.Approved);
        if (approvedEstimatesCount != 1)
        {
            throw new BusinessRuleViolationException("Work order requires exactly one approved estimate before completion.");
        }

        TransitionTo(WorkOrderStatus.Completed);
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

        if (estimate.InventoryLines.Count == 0 && estimate.ServiceLines.Count == 0)
        {
            throw new BusinessRuleViolationException("Estimate must contain at least one line item before submission.");
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

    private void TransitionTo(WorkOrderStatus newStatus)
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
        UpdatedAt = DateTime.UtcNow;

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
