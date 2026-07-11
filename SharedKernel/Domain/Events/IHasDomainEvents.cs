namespace GarageFlow.SharedKernel.Domain.Events;

public interface IHasDomainEvents
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
