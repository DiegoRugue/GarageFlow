using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CompleteEstimateService;

public sealed class CompleteEstimateServiceHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<CompleteEstimateServiceCommand, Unit>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<Unit> Handle(CompleteEstimateServiceCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var estimateId = EstimateId.From(request.EstimateId);
        var lineId = EstimateServiceLineId.From(request.LineId);

        var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        workOrder.CompleteEstimateService(estimateId, lineId);
        return Unit.Value;
    }
}
