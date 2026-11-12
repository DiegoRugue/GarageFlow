using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.ReceiveExecutionScheduleDecision;

public sealed record ReceiveExecutionScheduleDecisionCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid ExecutionId,
    Guid SagaId,
    bool Accepted) : ICommand, ICorrelatedCommand
{
    public string CorrelationId => SagaId.ToString("D");
}
