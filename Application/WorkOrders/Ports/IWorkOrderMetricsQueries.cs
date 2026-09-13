using GarageFlow.Application.WorkOrders.ReadModels;

namespace GarageFlow.Application.WorkOrders.Ports;

public interface IWorkOrderMetricsQueries
{
    Task<WorkOrderDailyMetricsReadModel> GetDailyAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}
