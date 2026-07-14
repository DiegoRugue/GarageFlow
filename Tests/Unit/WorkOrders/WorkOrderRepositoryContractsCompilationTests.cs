using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.WorkOrders.ReadModels;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Tests.Unit.WorkOrders;

public class WorkOrderRepositoryContractsCompilationTests
{
    private static readonly string[] ExpectedWorkOrderRepositoryMethodNames =
    [
        "AddAsync",
        "GetByIdAsync",
        "GetByIdForEstimateMutationAsync"
    ];

    [Fact]
    public void Task8_WorkOrderRepositoryAndQueryContracts_ShouldCompileAgainstExpectedSignatures()
    {
        Signature<Func<IWorkOrderRepository, WorkOrderId, CancellationToken, Task<WorkOrder?>>>(
            (repository, id, cancellationToken) => repository.GetByIdAsync(id, cancellationToken));
        Signature<Func<IWorkOrderRepository, WorkOrderId, CancellationToken, Task<WorkOrder?>>>(
            (repository, id, cancellationToken) => repository.GetByIdForEstimateMutationAsync(id, cancellationToken));
        Signature<Func<IWorkOrderRepository, WorkOrder, CancellationToken, Task>>(
            (repository, workOrder, cancellationToken) => repository.AddAsync(workOrder, cancellationToken));

        Signature<Func<IWorkOrderQueries, WorkOrderId, CancellationToken, Task<WorkOrderDetailsReadModel?>>>(
            (queries, id, cancellationToken) => queries.GetDetailsByIdAsync(id, cancellationToken));
        Signature<Func<IWorkOrderQueries, WorkOrderId, CancellationToken, Task<WorkOrderStatusReadModel?>>>(
            (queries, id, cancellationToken) => queries.GetStatusByIdAsync(id, cancellationToken));
        Signature<Func<IWorkOrderQueries, WorkOrderId, CustomerId, CancellationToken, Task<WorkOrderDetailsReadModel?>>>(
            (queries, id, customerId, cancellationToken) => queries.GetCustomerDetailsByIdAsync(id, customerId, cancellationToken));
        Signature<Func<IWorkOrderQueries, int, int, CustomerId?, CancellationToken, Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)>>>(
            (queries, page, pageSize, customerId, cancellationToken) => queries.ListActiveDetailsAsync(page, pageSize, customerId, cancellationToken));
        Signature<Func<IWorkOrderQueries, int, int, CustomerId, CancellationToken, Task<(IReadOnlyList<WorkOrderDetailsReadModel> Items, int TotalCount)>>>(
            (queries, page, pageSize, customerId, cancellationToken) => queries.ListCustomerDetailsAsync(page, pageSize, customerId, cancellationToken));
        Signature<Func<IWorkOrderQueries, DateTime, DateTime, ServiceId?, CancellationToken, Task<AverageServiceTimeReadModel>>>(
            (queries, completedFrom, completedTo, serviceId, cancellationToken) =>
                queries.GetAverageServiceTimeAsync(completedFrom, completedTo, serviceId, cancellationToken));

        Assert.Equal(
            ExpectedWorkOrderRepositoryMethodNames,
            typeof(IWorkOrderRepository).GetMethods().Select(method => method.Name).Order());
    }

    private static void Signature<TDelegate>(TDelegate _)
    {
    }
}
