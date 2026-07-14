using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.RejectMyEstimate;

public sealed class RejectMyEstimateHandler(
    IUserRepository userRepository,
    IWorkOrderRepository workOrderRepository,
    EstimateDecisionProcessor processor) : IRequestHandler<RejectMyEstimateCommand, Unit>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly EstimateDecisionProcessor _processor = processor ?? throw new ArgumentNullException(nameof(processor));

    public async ValueTask<Unit> Handle(RejectMyEstimateCommand request, CancellationToken cancellationToken)
    {
        var customerId = await CustomerWorkOrderAccess.GetRequiredCustomerIdAsync(_userRepository, request.UserId, cancellationToken);
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var workOrder = await CustomerWorkOrderAccess.GetRequiredEstimateMutationWorkOrderAsync(
            _workOrderRepository,
            workOrderId,
            customerId,
            request.WorkOrderId,
            cancellationToken);
        await _processor.RejectAsync(workOrder, EstimateId.From(request.EstimateId), cancellationToken);
        return Unit.Value;
    }
}
