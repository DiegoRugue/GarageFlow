namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Inbox;

public sealed class EstimateDecisionInboxEvent
{
    private EstimateDecisionInboxEvent() { }
    private EstimateDecisionInboxEvent(Guid eventId, string payloadHash, DateTime occurredAt, DateTime receivedAt)
    {
        EventId = eventId;
        PayloadHash = payloadHash;
        OccurredAt = occurredAt;
        ReceivedAt = receivedAt;
    }

    public Guid EventId { get; private set; }
    public string PayloadHash { get; private set; } = null!;
    public DateTime OccurredAt { get; private set; }
    public DateTime ReceivedAt { get; private set; }

    public static EstimateDecisionInboxEvent Create(Guid eventId, string payloadHash, DateTime occurredAt, DateTime receivedAt) =>
        new(eventId, payloadHash, occurredAt, receivedAt);
}
