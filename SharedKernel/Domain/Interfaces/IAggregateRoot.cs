using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.SharedKernel.Domain.Interfaces;

public interface IAggregateRoot
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
