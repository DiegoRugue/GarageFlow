using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderStatus;

public sealed class GetWorkOrderStatusHandler(
    IWorkOrderQueries workOrderQueries) : IRequestHandler<GetWorkOrderStatusQuery, GetWorkOrderStatusResult>
{
    private readonly IWorkOrderQueries _workOrderQueries = workOrderQueries ?? throw new ArgumentNullException(nameof(workOrderQueries));

    public async ValueTask<GetWorkOrderStatusResult> Handle(
        GetWorkOrderStatusQuery request,
        CancellationToken cancellationToken)
    {
        var status = await _workOrderQueries.GetStatusByIdAsync(WorkOrderId.From(request.Id), cancellationToken);
        if (status is null)
        {
            throw new NotFoundException($"Work order with ID '{request.Id}' was not found.");
        }

        return new GetWorkOrderStatusResult(status.Id, status.Status, status.UpdatedAt);
    }
}
