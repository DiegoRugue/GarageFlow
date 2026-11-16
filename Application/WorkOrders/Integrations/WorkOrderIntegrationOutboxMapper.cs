using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Domain.WorkOrders.Events;
using GarageFlow.SharedKernel.Domain.Events;

namespace GarageFlow.Application.WorkOrders.Integrations;

public sealed class WorkOrderIntegrationOutboxMapper : IIntegrationOutboxMapper
{
    public IReadOnlyList<IntegrationOutboxMessage> Map(
        IReadOnlyCollection<DomainEvent> domainEvents,
        string? correlationId)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        var messages = new List<IntegrationOutboxMessage>();
        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case WorkOrderStatusChanged status:
                    messages.Add(Map(status, correlationId));
                    break;
                case ExecutionSchedulingRequested schedule:
                    messages.Add(Map(schedule));
                    break;
                case ExecutionLifecycleRequested execution:
                    messages.Add(Map(execution));
                    break;
                case ExecutionChargeRequested charge:
                    messages.Add(Map(charge));
                    break;
            }
        }

        return messages;
    }

    private static IntegrationOutboxMessage Map(ExecutionSchedulingRequested schedule)
    {
        var workOrderId = schedule.WorkOrderId.Value;
        var payload = IntegrationEventJson.Serialize(new
        {
            schemaVersion = ExecutionSchedulingContract.SchemaVersion,
            messageType = ExecutionSchedulingContract.EventKey,
            messageId = schedule.ExecutionId,
            correlationId = workOrderId,
            occurredAt = schedule.RequestedAt,
            workOrderId,
            estimateId = schedule.EstimateId.Value,
            schedule.ExecutionId,
            payload = new { schedule.ServiceLineIds }
        });

        return new IntegrationOutboxMessage(
            schedule.ExecutionId,
            ExecutionSchedulingContract.EventKey,
            workOrderId,
            payload,
            schedule.RequestedAt,
            workOrderId.ToString("D"));
    }

    private static IntegrationOutboxMessage Map(ExecutionLifecycleRequested execution)
    {
        var workOrderId = execution.WorkOrderId.Value;
        var eventKey = execution.Complete ? ExecutionSchedulingContract.CompleteKey : ExecutionSchedulingContract.StartKey;
        var payload = IntegrationEventJson.Serialize(new
        {
            schemaVersion = ExecutionSchedulingContract.SchemaVersion,
            messageType = eventKey,
            execution.MessageId,
            correlationId = workOrderId,
            occurredAt = execution.RequestedAt,
            workOrderId,
            estimateId = execution.EstimateId.Value,
            execution.ExecutionId,
            payload = new { }
        });
        return new IntegrationOutboxMessage(
            execution.MessageId, eventKey, workOrderId, payload, execution.RequestedAt, workOrderId.ToString("D"));
    }

    private static IntegrationOutboxMessage Map(ExecutionChargeRequested charge)
    {
        var workOrderId = charge.WorkOrderId.Value;
        var payload = IntegrationEventJson.Serialize(new
        {
            schemaVersion = BillingChargeContract.SchemaVersion,
            messageType = BillingChargeContract.EventKey,
            charge.MessageId,
            correlationId = workOrderId,
            occurredAt = charge.RequestedAt,
            workOrderId,
            estimateId = charge.EstimateId.Value,
            charge.ChargeId,
            payload = new { amountInMinorUnits = decimal.Truncate(charge.Amount * 100m), currency = BillingChargeContract.Currency }
        });
        return new IntegrationOutboxMessage(
            charge.MessageId, BillingChargeContract.EventKey, workOrderId, payload, charge.RequestedAt, workOrderId.ToString("D"));
    }

    private static IntegrationOutboxMessage Map(
        WorkOrderStatusChanged domainEvent,
        string? correlationId)
    {
        var integrationEvent = new WorkOrderStatusChangedIntegrationEvent(
            domainEvent.WorkOrderId.Value,
            domainEvent.PreviousStatus.ToString(),
            domainEvent.NewStatus.ToString(),
            domainEvent.UpdatedAt);

        return new IntegrationOutboxMessage(
            Guid.NewGuid(),
            WorkOrderStatusChangedIntegrationEvent.EventKey,
            integrationEvent.WorkOrderId,
            IntegrationEventJson.Serialize(integrationEvent),
            integrationEvent.OccurredAt,
            correlationId);
    }
}
