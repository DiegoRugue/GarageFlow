using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Tests.Unit.WorkOrders;

public class WorkOrderRepositoryContractsCompilationTests
{
    [Fact]
    public void Task6_WorkOrderRepositoryContracts_ShouldCompileAgainstExpectedSignatures()
    {
        Signature<Func<IWorkOrderRepository, WorkOrderId, CancellationToken, Task<WorkOrder?>>>(
            (repository, id, cancellationToken) => repository.GetByIdAsync(id, cancellationToken));
        Signature<Func<IWorkOrderRepository, WorkOrderId, CancellationToken, Task<WorkOrderDetailsReadModel?>>>(
            (repository, id, cancellationToken) => repository.GetDetailsByIdAsync(id, cancellationToken));
        Signature<Func<IWorkOrderRepository, WorkOrderId, CustomerId, CancellationToken, Task<WorkOrderDetailsReadModel?>>>(
            (repository, id, customerId, cancellationToken) => repository.GetCustomerDetailsByIdAsync(id, customerId, cancellationToken));
        Signature<Func<IWorkOrderRepository, int, int, CustomerId?, CancellationToken, Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)>>>(
            (repository, page, pageSize, customerId, cancellationToken) => repository.ListDetailsAsync(page, pageSize, customerId, cancellationToken));
        Signature<Func<IWorkOrderRepository, int, int, CustomerId, CancellationToken, Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)>>>(
            (repository, page, pageSize, customerId, cancellationToken) => repository.ListCustomerDetailsAsync(page, pageSize, customerId, cancellationToken));
        Signature<Func<IWorkOrderRepository, WorkOrder, CancellationToken, Task>>(
            (repository, workOrder, cancellationToken) => repository.AddAsync(workOrder, cancellationToken));

        Assert.True(true);
    }

    private static void Signature<TDelegate>(TDelegate _)
    {
    }
}
