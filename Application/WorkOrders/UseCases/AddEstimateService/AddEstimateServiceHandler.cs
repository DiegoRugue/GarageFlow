using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Application.Services.Ports;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.AddEstimateService;

public sealed class AddEstimateServiceHandler(
    IWorkOrderRepository workOrderRepository,
    IServiceRepository serviceRepository) : IRequestHandler<AddEstimateServiceCommand, AddEstimateServiceResult>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly IServiceRepository _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));

    public async ValueTask<AddEstimateServiceResult> Handle(AddEstimateServiceCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var estimateId = EstimateId.From(request.EstimateId);
        var serviceId = ServiceId.From(request.ServiceId);
        var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        workOrder.EnsureEstimateCanBeEdited(estimateId);

        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
        if (service is null)
        {
            throw new NotFoundException($"Service with ID '{request.ServiceId}' was not found.");
        }

        workOrder.AddServiceLine(
            estimateId,
            service.Id,
            service.Description,
            service.Price);

        return new AddEstimateServiceResult(
            EstimateId: estimateId.Value,
            ServiceId: service.Id.Value,
            Description: service.Description.Value,
            UnitPrice: service.Price.Value,
            TotalPrice: service.Price.Value);
    }
}
