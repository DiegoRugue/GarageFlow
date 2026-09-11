using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Application.WorkOrders.Ports;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ListMyWorkOrders;

public sealed class ListMyWorkOrdersHandler(
    IUserRepository userRepository,
    ICustomerRepository customerRepository,
    IWorkOrderQueries workOrderQueries) : IRequestHandler<ListMyWorkOrdersQuery, ListMyWorkOrdersResult>
{
    private const int MaxPageSize = 100;

    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IWorkOrderQueries _workOrderQueries = workOrderQueries ?? throw new ArgumentNullException(nameof(workOrderQueries));

    public async ValueTask<ListMyWorkOrdersResult> Handle(ListMyWorkOrdersQuery request, CancellationToken cancellationToken)
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

        var customerId = await CustomerWorkOrderAccess.GetRequiredCustomerIdAsync(
            _userRepository,
            _customerRepository,
            request.UserId,
            cancellationToken);
        var (items, totalCount) = await _workOrderQueries.ListCustomerDetailsAsync(
            request.Page,
            request.PageSize,
            customerId,
            cancellationToken);

        return new ListMyWorkOrdersResult(
            Items: items.Select(WorkOrderDetailsMapper.MapCustomerDetails).ToList(),
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);
    }
}
