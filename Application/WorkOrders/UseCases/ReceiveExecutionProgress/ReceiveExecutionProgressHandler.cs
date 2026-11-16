using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ReceiveExecutionProgress;

public sealed class ReceiveExecutionProgressHandler(IWorkOrderRepository repository)
    : IRequestHandler<ReceiveExecutionProgressCommand, Unit>
{
    public async ValueTask<Unit> Handle(ReceiveExecutionProgressCommand request, CancellationToken cancellationToken)
    {
        if (request.ExecutionId == Guid.Empty || request.WorkOrderId != request.SagaId)
        {
            throw new ValidationException("Execution and saga identifiers must match the scheduling contract.");
        }

        var workOrder = await repository.GetByIdForEstimateMutationAsync(
            WorkOrderId.From(request.WorkOrderId), cancellationToken)
            ?? throw new NotFoundException("The work order for execution does not exist.");
        var estimateId = EstimateId.From(request.EstimateId);
        if (request.Completed)
        {
            workOrder.ApplyExecutionCompleted(estimateId, request.ExecutionId, request.OccurredAt);
        }
        else
        {
            workOrder.ApplyExecutionStarted(estimateId, request.ExecutionId, request.OccurredAt);
        }

        return Unit.Value;
    }
}
