using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Domain.WorkOrders.Repositories;

public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(WorkOrderId id, CancellationToken cancellationToken = default);

    Task<WorkOrder?> GetByIdForEstimateMutationAsync(WorkOrderId id, CancellationToken cancellationToken = default);

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

    Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken = default);
}
