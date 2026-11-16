namespace GarageFlow.Adapters.Api.WorkOrders.GetWorkOrderPayment;

public sealed record GetWorkOrderPaymentResponse(Guid ChargeId, string Status, string? CheckoutUrl, DateTime? ConfirmedAt);
