using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Users.Repositories;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetMyWorkOrderById;

public sealed class GetMyWorkOrderByIdHandler(
    IUserRepository userRepository,
    IWorkOrderRepository workOrderRepository) : IRequestHandler<GetMyWorkOrderByIdQuery, CustomerWorkOrderDetailsDto>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<CustomerWorkOrderDetailsDto> Handle(GetMyWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var customerId = await CustomerWorkOrderAccess.GetRequiredCustomerIdAsync(_userRepository, request.UserId, cancellationToken);
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var workOrder = await _workOrderRepository.GetCustomerDetailsByIdAsync(workOrderId, customerId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        return WorkOrderDetailsMapper.MapCustomerDetails(workOrder);
    }
}
