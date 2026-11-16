namespace GarageFlow.Application.WorkOrders.UseCases.GetWorkOrderPayment;

public sealed record GetWorkOrderPaymentResult(Guid ChargeId, string Status, string? CheckoutUrl, DateTime? ConfirmedAt);
