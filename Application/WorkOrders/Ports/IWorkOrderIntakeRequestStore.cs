namespace GarageFlow.Application.WorkOrders.Ports;

public interface IWorkOrderIntakeRequestStore
{
    Task<IntakeRequestClaim> ClaimAsync(
        Guid requestId,
        string payloadHash,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid requestId,
        Guid workOrderId,
        string responseJson,
        DateTime completedAt,
        CancellationToken cancellationToken = default);
}
