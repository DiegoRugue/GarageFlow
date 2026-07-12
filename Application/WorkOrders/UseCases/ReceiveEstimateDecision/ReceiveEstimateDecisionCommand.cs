using GarageFlow.Application.Common.Messaging;

namespace GarageFlow.Application.WorkOrders.UseCases.ReceiveEstimateDecision;

public sealed record ReceiveEstimateDecisionCommand(
    Guid EventId,
    Guid WorkOrderId,
    Guid EstimateId,
    string Decision,
    DateTime OccurredAt,
    string PayloadHash)
    : ICommand<ReceiveEstimateDecisionResult>, ICorrelatedCommand
{
    public string CorrelationId => EventId.ToString("D");
}
