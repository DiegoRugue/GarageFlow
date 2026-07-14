namespace GarageFlow.Adapters.Api.Webhooks.EstimateDecisions;

public sealed record EstimateDecisionWebhookRequest(
    Guid EventId,
    Guid WorkOrderId,
    Guid EstimateId,
    string Decision,
    DateTime OccurredAt);
