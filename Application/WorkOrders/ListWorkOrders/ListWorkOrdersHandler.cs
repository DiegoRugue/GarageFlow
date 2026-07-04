using GarageFlow.Application.WorkOrders.GetWorkOrderById;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.WorkOrders.Repositories;
using Mediator;

namespace GarageFlow.Application.WorkOrders.ListWorkOrders;

public sealed class ListWorkOrdersHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<ListWorkOrdersQuery, ListWorkOrdersResult>
{
    private const int MaxPageSize = 100;

    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<ListWorkOrdersResult> Handle(ListWorkOrdersQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1)
        {
            throw new ValidationException($"Page must be greater than or equal to 1. Received: {request.Page}.");
        }

        if (request.PageSize < 1)
        {
            throw new ValidationException($"PageSize must be greater than or equal to 1. Received: {request.PageSize}.");
        }

        if (request.PageSize > MaxPageSize)
        {
            throw new ValidationException($"PageSize cannot exceed {MaxPageSize}. Received: {request.PageSize}.");
        }

        CustomerId? customerId = request.CustomerId is null ? null : CustomerId.From(request.CustomerId.Value);
        var (items, totalCount) = await _workOrderRepository.ListDetailsAsync(
            request.Page,
            request.PageSize,
            customerId,
            cancellationToken);

        return new ListWorkOrdersResult(
            Items: items.Select(WorkOrderDetailsMapper.MapDetails).ToList(),
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }
}
