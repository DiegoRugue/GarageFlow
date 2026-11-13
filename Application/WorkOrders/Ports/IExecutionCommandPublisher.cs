namespace GarageFlow.Application.WorkOrders.Ports;

public interface IExecutionCommandPublisher
{
    Task PublishAsync(string payload, CancellationToken cancellationToken);
}
