using GarageFlow.BuildingBlocks.Domain.Events;

namespace GarageFlow.BuildingBlocks.Domain.Interfaces;

public interface IAggregateRoot
{
    IReadOnlyList<DomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
