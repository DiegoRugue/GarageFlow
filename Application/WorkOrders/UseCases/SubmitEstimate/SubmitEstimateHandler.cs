using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.SubmitEstimate;

public sealed class SubmitEstimateHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<SubmitEstimateCommand, Unit>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<Unit> Handle(SubmitEstimateCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var estimateId = EstimateId.From(request.EstimateId);

        var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        workOrder.SubmitEstimate(estimateId);

        return Unit.Value;
    }
}
