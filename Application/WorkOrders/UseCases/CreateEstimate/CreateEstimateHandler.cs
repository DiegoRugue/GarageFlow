using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.CreateEstimate;

public sealed class CreateEstimateHandler(
    IWorkOrderRepository workOrderRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateEstimateCommand, CreateEstimateResult>
{
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<CreateEstimateResult> Handle(CreateEstimateCommand request, CancellationToken cancellationToken)
    {
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
            if (workOrder is null)
            {
                throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
            }

            var estimate = workOrder.CreateEstimate();
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CreateEstimateResult(
                Id: estimate.Id.Value,
                WorkOrderId: workOrder.Id.Value,
                Status: estimate.Status.ToString(),
                TotalAmount: estimate.TotalAmount.Value,
                CreatedAt: estimate.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
