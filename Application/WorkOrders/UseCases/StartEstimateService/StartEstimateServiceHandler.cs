using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.StartEstimateService;

public sealed class StartEstimateServiceHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<StartEstimateServiceCommand, Unit>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<Unit> Handle(StartEstimateServiceCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var estimateId = EstimateId.From(request.EstimateId);
        var lineId = EstimateServiceLineId.From(request.LineId);

        var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        workOrder.StartEstimateService(estimateId, lineId);
        return Unit.Value;
    }
}
