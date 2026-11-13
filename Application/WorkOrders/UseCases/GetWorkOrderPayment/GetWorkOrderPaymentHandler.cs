using GarageFlow.Application.Customers.Ports;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Domain.WorkOrders.Enums;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderPayment;

public sealed class GetWorkOrderPaymentHandler(
    IWorkOrderRepository orders, IUserRepository users, ICustomerRepository customers)
    : IRequestHandler<GetWorkOrderPaymentQuery, GetWorkOrderPaymentResult>
{
    public async ValueTask<GetWorkOrderPaymentResult> Handle(
        GetWorkOrderPaymentQuery request, CancellationToken cancellationToken)
    {
        var orderId = WorkOrderId.From(request.Id);
        var customer = request.CustomerUserId is { } userId
            ? await CustomerWorkOrderAccess.GetRequiredCustomerIdAsync(users, customers, userId, cancellationToken)
            : (CustomerId?)null;
        var order = await orders.GetByIdAsync(orderId, cancellationToken);
        if (order?.ExecutionId is not { } chargeId || (customer.HasValue && order.CustomerId != customer.Value))
        {
            throw new NotFoundException("The work order payment does not exist.");
        }

        var status = order switch
        {
            { PaymentConfirmedAt: not null } => "Confirmed",
            { BillingPaymentRevision: > 0 } => "Revoked",
            { CheckoutUrl: not null } => "CheckoutAvailable",
            { Status: WorkOrderStatus.Completed } => "PendingCheckout",
            { Status: WorkOrderStatus.Cancelled or WorkOrderStatus.ExecutionScheduleRejected } => "Unavailable",
            _ => "AwaitingRepairCompletion"
        };
        return new GetWorkOrderPaymentResult(chargeId, status,
            status == "CheckoutAvailable" ? order.CheckoutUrl : null, order.PaymentConfirmedAt);
    }
}
