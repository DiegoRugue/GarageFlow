using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.RejectMyEstimate;

public sealed class RejectMyEstimateHandler(
    IUserRepository userRepository,
    IWorkOrderRepository workOrderRepository,
    IInventoryItemRepository inventoryItemRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RejectMyEstimateCommand, Unit>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly IInventoryItemRepository _inventoryItemRepository = inventoryItemRepository ?? throw new ArgumentNullException(nameof(inventoryItemRepository));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async ValueTask<Unit> Handle(RejectMyEstimateCommand request, CancellationToken cancellationToken)
    {
        var customerId = await CustomerWorkOrderAccess.GetRequiredCustomerIdAsync(_userRepository, request.UserId, cancellationToken);
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var workOrder = await CustomerWorkOrderAccess.GetRequiredEstimateMutationWorkOrderAsync(
                _workOrderRepository,
                workOrderId,
                customerId,
                request.WorkOrderId,
                cancellationToken);
            var rejectedEstimate = workOrder.RejectEstimate(EstimateId.From(request.EstimateId));
            foreach (var line in rejectedEstimate.InventoryLines)
            {
                var inventoryItem = await _inventoryItemRepository.GetByIdForStockReservationAsync(line.InventoryItemId, cancellationToken);
                if (inventoryItem is null)
                {
                    throw new NotFoundException($"Inventory item with ID '{line.InventoryItemId.Value}' was not found.");
                }

                inventoryItem.IncreaseStock(line.Quantity.Value);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Unit.Value;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
