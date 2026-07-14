using System.Diagnostics;
using System.Runtime.ExceptionServices;
using GarageFlow.Application.Common.Events;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.Common.Messaging;
using GarageFlow.SharedKernel.Domain.Events;
using GarageFlow.SharedKernel.Persistence;
using Mediator;

namespace GarageFlow.Application.Common.Behaviors;

public sealed class TransactionBehavior<TMessage, TResponse>(
    IUnitOfWork unitOfWork,
    IDomainEventDispatcher domainEventDispatcher,
    IIntegrationOutboxMapper integrationOutboxMapper,
    IOutboxWriter outboxWriter) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : GarageFlow.Application.Common.Messaging.ICommand<TResponse>, IMessage
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IDomainEventDispatcher _domainEventDispatcher =
        domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));
    private readonly IIntegrationOutboxMapper _integrationOutboxMapper =
        integrationOutboxMapper ?? throw new ArgumentNullException(nameof(integrationOutboxMapper));
    private readonly IOutboxWriter _outboxWriter = outboxWriter ?? throw new ArgumentNullException(nameof(outboxWriter));

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
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            domainEvents = _unitOfWork.DequeueDomainEvents();
            var correlationId = message is ICorrelatedCommand correlated
                ? correlated.CorrelationId
                : Activity.Current?.TraceId.ToHexString();
            var integrationMessages = _integrationOutboxMapper.Map(domainEvents, correlationId);

            if (integrationMessages.Count > 0)
            {
                await _outboxWriter.WriteAsync(integrationMessages, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            try
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            }
            catch
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            throw;
        }

        await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);

        return response;
    }
}
