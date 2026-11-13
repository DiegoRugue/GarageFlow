namespace GarageFlow.Application.WorkOrders.Common;

public static class BillingChargeContract
{
    public const string SchemaVersion = "1.0";
    public const string EventKey = "billing.create-charge.v1";
    public const string CheckoutCreatedKey = "billing.checkout-created.v1";
    public const string PaymentStateKey = "billing.payment-state.v1";
    public const string Currency = "BRL";
}
