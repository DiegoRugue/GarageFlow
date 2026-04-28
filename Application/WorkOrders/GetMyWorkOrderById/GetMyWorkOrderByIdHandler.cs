using GarageFlow.BuildingBlocks.Domain.Exceptions;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Domain.Users.Repositories;
using GarageFlow.Domain.Users.ValueObjects;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using Mediator;

namespace GarageFlow.Application.WorkOrders.GetMyWorkOrderById;

public sealed class GetMyWorkOrderByIdHandler(
    IUserRepository userRepository,
    IWorkOrderRepository workOrderRepository) : IRequestHandler<GetMyWorkOrderByIdQuery, CustomerWorkOrderDetailsDto>
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository ?? throw new ArgumentNullException(nameof(workOrderRepository));

    public async ValueTask<CustomerWorkOrderDetailsDto> Handle(GetMyWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = UserId.From(request.UserId);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException($"User with ID '{request.UserId}' was not found.");
        }

        var customerId = GetRequiredCustomerId(user);
        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var workOrder = await _workOrderRepository.GetCustomerDetailsByIdAsync(workOrderId, customerId, cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        return MapDetails(workOrder);
    }

    private static CustomerId GetRequiredCustomerId(User user)
    {
        if (user.Role != UserRole.Customer || user.CustomerId is null)
        {
            throw new UnauthorizedAccessException("Authenticated user is not a customer user.");
        }

        return user.CustomerId.Value;
    }

    private static CustomerWorkOrderDetailsDto MapDetails(WorkOrderDetailsReadModel workOrder)
    {
        return new CustomerWorkOrderDetailsDto(
            Id: workOrder.Id,
            CustomerId: workOrder.CustomerId,
            VehicleId: workOrder.VehicleId,
            Status: workOrder.Status,
            CreatedAt: workOrder.CreatedAt,
            UpdatedAt: workOrder.UpdatedAt,
            Estimates: workOrder.Estimates.Select(MapEstimate).ToList());
    }

    private static CustomerWorkOrderEstimateDto MapEstimate(WorkOrderEstimateReadModel estimate)
    {
        return new CustomerWorkOrderEstimateDto(
            Id: estimate.Id,
            WorkOrderId: estimate.WorkOrderId,
            Status: estimate.Status,
            TotalAmount: estimate.TotalAmount,
            CreatedAt: estimate.CreatedAt,
            UpdatedAt: estimate.UpdatedAt,
            InventoryLines: estimate.InventoryLines.Select(MapInventoryLine).ToList(),
            ServiceLines: estimate.ServiceLines.Select(MapServiceLine).ToList());
    }

    private static CustomerWorkOrderInventoryLineDto MapInventoryLine(WorkOrderInventoryLineReadModel line)
    {
        return new CustomerWorkOrderInventoryLineDto(
            Id: line.Id,
            EstimateId: line.EstimateId,
            InventoryItemId: line.InventoryItemId,
            Description: line.DescriptionSnapshot,
            Quantity: line.Quantity,
            UnitPrice: line.UnitPrice,
            TotalPrice: line.TotalPrice);
    }

    private static CustomerWorkOrderServiceLineDto MapServiceLine(WorkOrderServiceLineReadModel line)
    {
        return new CustomerWorkOrderServiceLineDto(
            Id: line.Id,
            EstimateId: line.EstimateId,
            ServiceId: line.ServiceId,
            Description: line.DescriptionSnapshot,
            UnitPrice: line.UnitPrice,
            TotalPrice: line.TotalPrice);
    }
}
