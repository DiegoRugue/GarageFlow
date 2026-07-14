using GarageFlow.Application.WorkOrders.Integrations;

namespace GarageFlow.Application.WorkOrders.Ports;

public interface IWorkOrderStatusNotificationPublisher
{
    Task PublishAsync(
        WorkOrderStatusChangedIntegrationEvent notification,
        CancellationToken cancellationToken);
}
