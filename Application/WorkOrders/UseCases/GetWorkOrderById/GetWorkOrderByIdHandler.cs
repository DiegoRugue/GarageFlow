using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderById;

public sealed class GetWorkOrderByIdHandler(
    IWorkOrderQueries workOrderQueries) : IRequestHandler<GetWorkOrderByIdQuery, WorkOrderDetailsDto>
{
    private readonly IWorkOrderQueries _workOrderQueries = workOrderQueries ?? throw new ArgumentNullException(nameof(workOrderQueries));

    public async ValueTask<WorkOrderDetailsDto> Handle(GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.Id);
        var workOrder = await _workOrderQueries.GetDetailsByIdAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.Id}' was not found.");
        }

        return WorkOrderDetailsMapper.MapDetails(workOrder);
    }
}
