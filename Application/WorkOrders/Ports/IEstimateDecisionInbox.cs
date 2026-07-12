using GarageFlow.Application.WorkOrders.Common;

namespace GarageFlow.Application.WorkOrders.Ports;

public interface IEstimateDecisionInbox
{
    Task<EstimateDecisionInboxRegistration> RegisterAsync(
        Guid eventId,
        string payloadHash,
        DateTime occurredAt,
        DateTime receivedAt,
        CancellationToken cancellationToken = default);
}
