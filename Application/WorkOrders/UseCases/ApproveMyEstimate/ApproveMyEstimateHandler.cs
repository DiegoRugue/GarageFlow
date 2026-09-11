using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ApproveMyEstimate;

public sealed class ApproveMyEstimateHandler(
    IUserRepository userRepository,
    ICustomerRepository customerRepository,
    IWorkOrderRepository workOrderRepository) : IRequestHandler<ApproveMyEstimateCommand, Unit>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly ICustomerRepository _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<Unit> Handle(ApproveMyEstimateCommand request, CancellationToken cancellationToken)
    {
        var customerId = await CustomerWorkOrderAccess.GetRequiredCustomerIdAsync(
            _userRepository,
            _customerRepository,
            request.UserId,
            cancellationToken);
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var workOrder = await CustomerWorkOrderAccess.GetRequiredEstimateMutationWorkOrderAsync(
            _workOrderRepository,
            workOrderId,
            customerId,
            request.WorkOrderId,
            cancellationToken);
        workOrder.ApproveEstimate(EstimateId.From(request.EstimateId));
        return Unit.Value;
    }
}
