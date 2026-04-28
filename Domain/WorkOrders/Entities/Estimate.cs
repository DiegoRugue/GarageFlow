using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.InventoryItems.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Entities;

public sealed class Estimate : Entity<EstimateId>
{
    private readonly List<EstimateInventoryLine> _inventoryLines = [];
    private readonly List<EstimateServiceLine> _serviceLines = [];

    public WorkOrderId WorkOrderId { get; private set; }
    public EstimateStatus Status { get; private set; }
    public IReadOnlyCollection<EstimateInventoryLine> InventoryLines => _inventoryLines.AsReadOnly();
    public IReadOnlyCollection<EstimateServiceLine> ServiceLines => _serviceLines.AsReadOnly();
    public Price TotalAmount => Price.Create(
        _inventoryLines.Sum(line => line.TotalPrice.Value) +
        _serviceLines.Sum(line => line.TotalPrice.Value));

    private Estimate(EstimateId id, WorkOrderId workOrderId) : base(id)
    {
        WorkOrderId = EnsureValidWorkOrderId(workOrderId);
        Status = EstimateStatus.Draft;
    }

    public static Estimate Create(WorkOrderId workOrderId)
    {
        return new Estimate(EstimateId.New(), workOrderId);
    }

    public EstimateInventoryLine AddInventoryLine(
        InventoryItemId inventoryItemId,
        Description description,
        EstimateItemQuantity quantity,
        Price unitCost,
        Price unitPrice)
    {
        EnsureEditable();

        var line = EstimateInventoryLine.Create(
            Id,
            inventoryItemId,
            description,
            quantity,
            unitCost,
            unitPrice);

        _inventoryLines.Add(line);
        UpdatedAt = DateTime.UtcNow;

        return line;
    }

    public EstimateServiceLine AddServiceLine(
        ServiceId serviceId,
        Description description,
        Price unitPrice)
    {
        EnsureEditable();

        var line = EstimateServiceLine.Create(
            Id,
            serviceId,
            description,
            unitPrice);

        _serviceLines.Add(line);
        UpdatedAt = DateTime.UtcNow;

        return line;
    }

    public void Submit()
    {
        EnsureEditable();

        if (_inventoryLines.Count == 0 && _serviceLines.Count == 0)
        {
            throw new BusinessRuleViolationException("Estimate must contain at least one line item before submission.");
        }

        TransitionTo(EstimateStatus.Pending);
    }

    public void Approve()
    {
        if (Status != EstimateStatus.Pending)
        {
            throw new BusinessRuleViolationException("Only pending estimates can be approved.");
        }

        TransitionTo(EstimateStatus.Approved);
    }

    public void Reject()
    {
        if (Status != EstimateStatus.Pending)
        {
            throw new BusinessRuleViolationException("Only pending estimates can be rejected.");
        }

        TransitionTo(EstimateStatus.Rejected);
    }

    public void Cancel()
    {
        if (Status != EstimateStatus.Draft)
        {
            throw new BusinessRuleViolationException("Only draft estimates can be cancelled.");
        }

        TransitionTo(EstimateStatus.Cancelled);
    }

    private void EnsureEditable()
    {
        if (Status != EstimateStatus.Draft)
        {
            throw new BusinessRuleViolationException("Only draft estimates can be edited.");
        }
    }

    private void TransitionTo(EstimateStatus newStatus)
    {
        if (Status == newStatus)
        {
            return;
        }

        var isAllowed = (Status, newStatus) switch
        {
            (EstimateStatus.Draft, EstimateStatus.Pending) => true,
            (EstimateStatus.Draft, EstimateStatus.Cancelled) => true,
            (EstimateStatus.Pending, EstimateStatus.Approved) => true,
            (EstimateStatus.Pending, EstimateStatus.Rejected) => true,
            _ => false
        };

        if (!isAllowed)
        {
            throw new BusinessRuleViolationException($"Estimate status transition from '{Status}' to '{newStatus}' is not allowed.");
        }

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    private static WorkOrderId EnsureValidWorkOrderId(WorkOrderId workOrderId)
    {
        if (workOrderId.Value == Guid.Empty)
        {
            throw new ValidationException("Work order identifier cannot be empty.");
        }

        return workOrderId;
    }
}
