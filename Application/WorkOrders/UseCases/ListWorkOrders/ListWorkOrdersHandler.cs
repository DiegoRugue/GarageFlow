using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderById;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Application.WorkOrders.Ports;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ListWorkOrders;

public sealed class ListWorkOrdersHandler(
    IWorkOrderQueries workOrderQueries) : IRequestHandler<ListWorkOrdersQuery, ListWorkOrdersResult>
{
    private const int MaxPageSize = 100;

    private readonly IWorkOrderQueries _workOrderQueries = workOrderQueries ?? throw new ArgumentNullException(nameof(workOrderQueries));

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
        var (items, totalCount) = await _workOrderQueries.ListActiveDetailsAsync(
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
