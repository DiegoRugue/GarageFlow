using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Users.Repositories;
using GarageFlow.Domain.WorkOrders.Repositories;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ListMyWorkOrders;

public sealed class ListMyWorkOrdersHandler(
    IUserRepository userRepository,
    IWorkOrderRepository workOrderRepository) : IRequestHandler<ListMyWorkOrdersQuery, ListMyWorkOrdersResult>
{
    private const int MaxPageSize = 100;

    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

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

        var customerId = await CustomerWorkOrderAccess.GetRequiredCustomerIdAsync(_userRepository, request.UserId, cancellationToken);
        var (items, totalCount) = await _workOrderRepository.ListCustomerDetailsAsync(
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
