namespace GarageFlow.SharedKernel.Domain.Events;

public abstract record DomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
