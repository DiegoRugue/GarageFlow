using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CancelWorkOrder;

public sealed class CancelWorkOrderHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<CancelWorkOrderCommand, Unit>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<Unit> Handle(CancelWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        workOrder.Cancel();
        return Unit.Value;
    }
}
