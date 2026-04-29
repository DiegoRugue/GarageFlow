using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.BuildingBlocks.Persistence;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.InventoryItems.Repositories;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.Repositories;
using GarageFlow.Domain.Users.ValueObjects;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.RejectMyEstimate;

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
        var userId = UserId.From(request.UserId);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException($"User with ID '{request.UserId}' was not found.");
        }

        var customerId = GetRequiredCustomerId(user);
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
            if (workOrder is null)
            {
                throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
            }

            if (workOrder.CustomerId != customerId)
            {
                throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
            }

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

    private static CustomerId GetRequiredCustomerId(User user)
    {
        if (user.Role != UserRole.Customer || user.CustomerId is null)
        {
            throw new UnauthorizedAccessException("Authenticated user is not a customer user.");
        }

        return user.CustomerId.Value;
    }
}
