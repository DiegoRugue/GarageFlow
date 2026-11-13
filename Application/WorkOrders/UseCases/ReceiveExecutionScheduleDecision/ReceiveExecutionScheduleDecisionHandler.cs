using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ReceiveExecutionScheduleDecision;

public sealed class ReceiveExecutionScheduleDecisionHandler(
    IWorkOrderRepository repository,
    EstimateDecisionProcessor processor) : IRequestHandler<ReceiveExecutionScheduleDecisionCommand, Unit>
{
    public async ValueTask<Unit> Handle(ReceiveExecutionScheduleDecisionCommand request, CancellationToken cancellationToken)
    {
        if (request.ExecutionId == Guid.Empty || request.WorkOrderId != request.SagaId)
        {
            throw new ValidationException("Execution and saga identifiers must match the scheduling contract.");
        }

        var workOrder = await repository.GetByIdForEstimateMutationAsync(
            WorkOrderId.From(request.WorkOrderId), cancellationToken)
            ?? throw new NotFoundException("The work order for the execution decision does not exist.");
        var reservations = workOrder.ApplyExecutionScheduleDecision(
            EstimateId.From(request.EstimateId), request.ExecutionId, request.Accepted);
        await processor.ReleaseAsync(reservations, cancellationToken);
        return Unit.Value;
    }
}
