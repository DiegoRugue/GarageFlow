using GarageFlow.Application.WorkOrders.ReadModels;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Application.WorkOrders.Ports;

public interface IWorkOrderQueries
{
    Task<WorkOrderDetailsReadModel?> GetDetailsByIdAsync(
        WorkOrderId id,
        CancellationToken cancellationToken = default);

    Task<WorkOrderDetailsReadModel?> GetCustomerDetailsByIdAsync(
        WorkOrderId id,
        CustomerId customerId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListDetailsAsync(
        int page,
        int pageSize,
        CustomerId? customerId = null,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)> ListCustomerDetailsAsync(
        int page,
        int pageSize,
        CustomerId customerId,
        CancellationToken cancellationToken = default);

    Task<AverageServiceTimeReadModel> GetAverageServiceTimeAsync(
        DateTime completedFrom,
        DateTime completedTo,
        ServiceId? serviceId = null,
        CancellationToken cancellationToken = default);
}
