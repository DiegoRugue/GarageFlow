using GarageFlow.Application.Common.Messaging;
using GarageFlow.Domain.WorkOrders.Enums;

namespace GarageFlow.Application.WorkOrders.UseCases.ReceiveBillingResult;

public sealed record ReceiveBillingResultCommand(
    Guid WorkOrderId,
    Guid EstimateId,
    Guid ChargeId,
    Guid SagaId,
    decimal AmountInMinorUnits,
    string Currency,
    DateTime OccurredAt,
    string? CheckoutUrl,
    long PaymentRevision,
    WorkOrderPaymentState PaymentState) : ICommand, ICorrelatedCommand
{
    public string CorrelationId => SagaId.ToString("D");
}
