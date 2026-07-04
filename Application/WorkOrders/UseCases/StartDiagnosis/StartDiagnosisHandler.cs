using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.StartDiagnosis;

public sealed class StartDiagnosisHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<StartDiagnosisCommand, Unit>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<Unit> Handle(StartDiagnosisCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        workOrder.StartDiagnosis();
        return Unit.Value;
    }
}
