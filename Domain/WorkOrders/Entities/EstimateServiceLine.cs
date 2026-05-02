using GarageFlow.BuildingBlocks.Domain.Entities;
using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Entities;

public sealed class EstimateServiceLine : Entity<EstimateServiceLineId>
{
    public EstimateId EstimateId { get; private set; }
    public ServiceId ServiceId { get; private set; }
    public Description DescriptionSnapshot { get; private set; }
    public Price UnitPrice { get; private set; }
    public EstimateServiceLineStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Price TotalPrice => Price.Create(UnitPrice.Value);

    private EstimateServiceLine(
        EstimateServiceLineId id,
        EstimateId estimateId,
        ServiceId serviceId,
        Description descriptionSnapshot,
        Price unitPrice) : base(id)
    {
        EstimateId = EnsureValidEstimateId(estimateId);
        ServiceId = EnsureValidServiceId(serviceId);
        DescriptionSnapshot = EnsureDescription(descriptionSnapshot);
        UnitPrice = EnsurePrice(unitPrice);
        Status = EstimateServiceLineStatus.Pending;
    }

    public static EstimateServiceLine Create(
        EstimateId estimateId,
        ServiceId serviceId,
        Description description,
        Price unitPrice)
    {
        return new EstimateServiceLine(
            EstimateServiceLineId.New(),
            estimateId,
            serviceId,
            description,
            unitPrice);
    }

    internal void Start()
    {
        var startedAt = DateTime.UtcNow;
        TransitionTo(EstimateServiceLineStatus.InProgress, startedAt);
        StartedAt = startedAt;
    }

    internal void Complete()
    {
        var completedAt = DateTime.UtcNow;
        TransitionTo(EstimateServiceLineStatus.Completed, completedAt);
        CompletedAt = completedAt;
    }

    private void TransitionTo(EstimateServiceLineStatus newStatus, DateTime occurredAt)
    {
        var isAllowed = (Status, newStatus) switch
        {
            (EstimateServiceLineStatus.Pending, EstimateServiceLineStatus.InProgress) => true,
            (EstimateServiceLineStatus.InProgress, EstimateServiceLineStatus.Completed) => true,
            _ => false
        };

        if (!isAllowed)
        {
            throw new BusinessRuleViolationException($"Estimate service line status transition from '{Status}' to '{newStatus}' is not allowed.");
        }

        Status = newStatus;
        UpdatedAt = occurredAt;
    }

    private static EstimateId EnsureValidEstimateId(EstimateId estimateId)
    {
        if (estimateId.Value == Guid.Empty)
        {
            throw new ValidationException("Estimate identifier cannot be empty.");
        }

        return estimateId;
    }

    private static ServiceId EnsureValidServiceId(ServiceId serviceId)
    {
        if (serviceId.Value == Guid.Empty)
        {
            throw new ValidationException("Service identifier cannot be empty.");
        }

        return serviceId;
    }

    private static Description EnsureDescription(Description description)
    {
        if (description is null)
        {
            throw new ValidationException("Estimate service line description cannot be null.");
        }

        return description;
    }

    private static Price EnsurePrice(Price price)
    {
        if (price is null)
        {
            throw new ValidationException("Estimate service line price cannot be null.");
        }

        return price;
    }
}
