using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.GetWorkOrderById;

public sealed class GetWorkOrderByIdHandler(
    IWorkOrderRepository workOrderRepository) : IRequestHandler<GetWorkOrderByIdQuery, WorkOrderDetailsDto>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<WorkOrderDetailsDto> Handle(GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.Id);
        var workOrder = await _workOrderRepository.GetDetailsByIdAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.Id}' was not found.");
        }

        return WorkOrderDetailsMapper.MapDetails(workOrder);
    }
}
