namespace GarageFlow.Application.WorkOrders.Integrations;

public sealed record WorkOrderStatusChangedIntegrationEvent(
    Guid WorkOrderId,
    string PreviousStatus,
    string CurrentStatus,
    DateTime OccurredAt)
{
    public const string EventKey = "work-order.status-changed.v1";
}
