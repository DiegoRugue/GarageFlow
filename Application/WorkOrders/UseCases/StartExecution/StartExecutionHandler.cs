using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.StartExecution;

public sealed class StartExecutionHandler(IWorkOrderRepository repository) : IRequestHandler<StartExecutionCommand, Unit>
{
    public async ValueTask<Unit> Handle(StartExecutionCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdForEstimateMutationAsync(
            WorkOrderId.From(request.WorkOrderId), cancellationToken)
            ?? throw new NotFoundException("The work order for execution does not exist.");
        workOrder.RequestExecutionStart();
        return Unit.Value;
    }
}
