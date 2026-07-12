using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ApproveMyEstimate;

public sealed class ApproveMyEstimateHandler(
    IUserRepository userRepository,
    IWorkOrderRepository workOrderRepository,
    EstimateDecisionProcessor processor) : IRequestHandler<ApproveMyEstimateCommand, Unit>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly EstimateDecisionProcessor _processor = processor ?? throw new ArgumentNullException(nameof(processor));

    public async ValueTask<Unit> Handle(ApproveMyEstimateCommand request, CancellationToken cancellationToken)
    {
        var customerId = await CustomerWorkOrderAccess.GetRequiredCustomerIdAsync(_userRepository, request.UserId, cancellationToken);
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var workOrder = await CustomerWorkOrderAccess.GetRequiredEstimateMutationWorkOrderAsync(
            _workOrderRepository,
            workOrderId,
            customerId,
            request.WorkOrderId,
            cancellationToken);
        _processor.Approve(workOrder, EstimateId.From(request.EstimateId));
        return Unit.Value;
    }
}
