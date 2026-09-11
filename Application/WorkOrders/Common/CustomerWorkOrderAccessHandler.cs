using GarageFlow.SharedKernel.Domain.Exceptions;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.Customers.Enums;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Domain.Users.ValueObjects;
using GarageFlow.Domain.WorkOrders.Entities;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;

namespace GarageFlow.Application.WorkOrders.Common;

internal static class CustomerWorkOrderAccess
{
    public static async Task<CustomerId> GetRequiredCustomerIdAsync(
        IUserRepository userRepository,
        ICustomerRepository customerRepository,
        Guid userIdValue,
        CancellationToken cancellationToken)
    {
        var userId = UserId.From(userIdValue);
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException($"User with ID '{userIdValue}' was not found.");
        }

        if (user.Role != UserRole.Customer || user.CustomerId is null)
        {
            throw new UnauthorizedAccessException("Authenticated user is not a customer user.");
        }

        var customer = await customerRepository.GetByIdAsync(user.CustomerId.Value, cancellationToken);
        if (customer is null || customer.Status != CustomerStatus.Active)
        {
            throw new UnauthorizedAccessException("Customer access is unavailable.");
        }

        return user.CustomerId.Value;
    }

    public static async Task<WorkOrder> GetRequiredEstimateMutationWorkOrderAsync(
        IWorkOrderRepository workOrderRepository,
        WorkOrderId workOrderId,
        CustomerId customerId,
        Guid workOrderIdValue,
        CancellationToken cancellationToken)
    {
        var workOrder = await workOrderRepository.GetByIdForEstimateMutationAsync(workOrderId, cancellationToken);
        if (workOrder is null || workOrder.CustomerId != customerId)
        {
            throw new NotFoundException($"Work order with ID '{workOrderIdValue}' was not found.");
        }

        return workOrder;
    }
}
