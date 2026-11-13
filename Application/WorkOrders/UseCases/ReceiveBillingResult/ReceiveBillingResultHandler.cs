using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ReceiveBillingResult;

public sealed class ReceiveBillingResultHandler(IWorkOrderRepository repository)
    : IRequestHandler<ReceiveBillingResultCommand, Unit>
{
    public async ValueTask<Unit> Handle(ReceiveBillingResultCommand request, CancellationToken cancellationToken)
    {
        if (request.ChargeId == Guid.Empty || request.WorkOrderId != request.SagaId ||
            request.Currency != BillingChargeContract.Currency)
        {
            throw new ValidationException("The billing result identity or currency is invalid.");
        }

        var order = await repository.GetByIdForEstimateMutationAsync(
            WorkOrderId.From(request.WorkOrderId), cancellationToken)
            ?? throw new NotFoundException("The work order for the billing result does not exist.");
        var estimateId = EstimateId.From(request.EstimateId);
        if (request.CheckoutUrl is { } checkoutUrl)
        {
            order.ApplyBillingCheckout(estimateId, request.ChargeId, request.AmountInMinorUnits, checkoutUrl);
        }
        else
        {
            order.ApplyBillingPayment(estimateId, request.ChargeId, request.AmountInMinorUnits,
                request.PaymentRevision, request.PaymentState, request.OccurredAt);
        }

        return Unit.Value;
    }
}
