using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetMyWorkOrderById;

public sealed class GetMyWorkOrderByIdHandler(
    IUserRepository userRepository,
    ICustomerRepository customerRepository,
    IWorkOrderQueries workOrderQueries) : IRequestHandler<GetMyWorkOrderByIdQuery, CustomerWorkOrderDetailsDto>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IWorkOrderQueries _workOrderQueries = workOrderQueries ?? throw new ArgumentNullException(nameof(workOrderQueries));

    public async ValueTask<CustomerWorkOrderDetailsDto> Handle(GetMyWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var customerId = await CustomerWorkOrderAccess.GetRequiredCustomerIdAsync(
            _userRepository,
            _customerRepository,
            request.UserId,
            cancellationToken);
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var workOrder = await _workOrderQueries.GetCustomerDetailsByIdAsync(workOrderId, customerId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        return WorkOrderDetailsMapper.MapCustomerDetails(workOrder);
    }
}
