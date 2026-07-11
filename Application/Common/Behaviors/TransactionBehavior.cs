using GarageFlow.Application.Common.Events;
using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.SharedKernel.Persistence;
using Mediator;

namespace GarageFlow.Application.Common.Behaviors;

public sealed class TransactionBehavior<TMessage, TResponse>(
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher domainEventDispatcher) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : GarageFlow.Application.Common.Messaging.ICommand<TResponse>, IMessage
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IDomainEventDispatcher _domainEventDispatcher =
        domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        TResponse response;
        IReadOnlyList<DomainEvent> domainEvents;

        try
        {
            response = await next(message, cancellationToken);
            domainEvents = _unitOfWork.DequeueDomainEvents();

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);

        return response;
    }
}
