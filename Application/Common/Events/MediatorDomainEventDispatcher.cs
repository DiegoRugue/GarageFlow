using GarageFlow.SharedKernel.Domain.Events;
using Mediator;

namespace GarageFlow.Application.Common.Events;

public sealed class MediatorDomainEventDispatcher(IMediator mediator) : IDomainEventDispatcher
{
    private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    public async ValueTask DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(new DomainEventNotification(domainEvent), cancellationToken);
        }
    }
}
