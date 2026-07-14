using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CancelWorkOrder;

public sealed class CancelWorkOrderHandler(
    IWorkOrderRepository workOrderRepository,
    EstimateDecisionProcessor processor) : IRequestHandler<CancelWorkOrderCommand, Unit>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly EstimateDecisionProcessor _processor = processor ?? throw new ArgumentNullException(nameof(processor));

    public async ValueTask<Unit> Handle(CancelWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        var reservations = workOrder.Cancel();
        await _processor.ReleaseAsync(reservations, cancellationToken);
        return Unit.Value;
    }
}
