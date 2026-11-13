using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.ReceiveExecutionProgress;

public sealed record ReceiveExecutionProgressCommand(
    Guid WorkOrderId, Guid EstimateId, Guid ExecutionId, Guid SagaId, DateTime OccurredAt, bool Completed)
    : ICommand, ICorrelatedCommand
{
    public string CorrelationId => SagaId.ToString("D");
}
