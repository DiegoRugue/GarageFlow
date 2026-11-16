namespace GarageFlow.Application.WorkOrders.Ports;

public interface IBillingChargePublisher
{
    Task PublishAsync(string payload, CancellationToken cancellationToken);
}
