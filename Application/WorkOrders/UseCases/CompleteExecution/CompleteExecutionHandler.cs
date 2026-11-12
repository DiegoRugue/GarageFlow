using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CompleteExecution;

public sealed class CompleteExecutionHandler(IWorkOrderRepository repository) : IRequestHandler<CompleteExecutionCommand, Unit>
{
    public async ValueTask<Unit> Handle(CompleteExecutionCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdForEstimateMutationAsync(
            WorkOrderId.From(request.WorkOrderId), cancellationToken)
            ?? throw new NotFoundException("The work order for execution does not exist.");
        workOrder.RequestExecutionCompletion();
        return Unit.Value;
    }
}
