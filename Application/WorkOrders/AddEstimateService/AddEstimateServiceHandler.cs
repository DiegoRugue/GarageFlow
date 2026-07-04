using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Services.Repositories;
using GarageFlow.Domain.Services.ValueObjects;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.AddEstimateService;

public sealed class AddEstimateServiceHandler(
    IWorkOrderRepository workOrderRepository,
    IServiceRepository serviceRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<AddEstimateServiceCommand, AddEstimateServiceResult>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly IServiceRepository _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<AddEstimateServiceResult> Handle(AddEstimateServiceCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var estimateId = EstimateId.From(request.EstimateId);
        var serviceId = ServiceId.From(request.ServiceId);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
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
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new AddEstimateServiceResult(
                EstimateId: estimateId.Value,
                ServiceId: service.Id.Value,
                Description: service.Description.Value,
                UnitPrice: service.Price.Value,
                TotalPrice: service.Price.Value);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
