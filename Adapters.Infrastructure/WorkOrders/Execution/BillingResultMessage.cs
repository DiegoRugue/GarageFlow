using System.Text.Json;
using GarageFlow.Application.Common.Messaging;
using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.UseCases.ReceiveBillingResult;
using GarageFlow.Domain.WorkOrders.Enums;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Execution;

internal sealed record BillingResultMessage(
    string SchemaVersion,
    string MessageType,
    string MessageId,
    string CorrelationId,
    string OccurredAt,
    string WorkOrderId,
    string EstimateId,
    string ChargeId,
    JsonElement Payload)
{
    public static ICommand ReadCommand(string body)
    {
        var result = JsonSerializer.Deserialize<BillingResultMessage>(body, WorkOrderResultJson.Options)
            ?? throw new JsonException("A billing result is required.");
        if (result.SchemaVersion != BillingChargeContract.SchemaVersion ||
            !WorkOrderResultJson.TryReadTimestamp(result.OccurredAt, out var occurredAt) ||
            result.Payload.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Invalid billing result envelope.");
        }

        _ = WorkOrderResultJson.ReadId(result.MessageId);
        var workOrderId = WorkOrderResultJson.ReadId(result.WorkOrderId);
        var estimateId = WorkOrderResultJson.ReadId(result.EstimateId);
        var chargeId = WorkOrderResultJson.ReadId(result.ChargeId);
        var sagaId = WorkOrderResultJson.ReadId(result.CorrelationId);

        if (!result.Payload.TryGetProperty("amountInMinorUnits", out var amountNode) ||
            amountNode.ValueKind != JsonValueKind.Number || !amountNode.TryGetDecimal(out var amount) ||
            amount < 0 || amount != decimal.Truncate(amount) ||
            !result.Payload.TryGetProperty("currency", out var currencyNode) ||
            currencyNode.ValueKind != JsonValueKind.String || !currencyNode.ValueEquals(BillingChargeContract.Currency))
        {
            throw new JsonException("Invalid billing amount or currency.");
        }

        if (result.MessageType == BillingChargeContract.CheckoutCreatedKey &&
            result.Payload.GetPropertyCount() == 3 &&
            result.Payload.TryGetProperty("checkoutUrl", out var url) &&
            url.ValueKind == JsonValueKind.String)
        {
            return new ReceiveBillingResultCommand(workOrderId, estimateId, chargeId, sagaId, amount,
                BillingChargeContract.Currency, occurredAt.UtcDateTime, url.GetString(), 0, default);
        }

        if (result.MessageType != BillingChargeContract.PaymentStateKey ||
            result.Payload.GetPropertyCount() != 4 ||
            !result.Payload.TryGetProperty("revision", out var revisionNode) ||
            revisionNode.ValueKind != JsonValueKind.Number || !revisionNode.TryGetInt64(out var revision) || revision <= 0 ||
            !result.Payload.TryGetProperty("state", out var stateNode) || stateNode.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Invalid billing payment state result.");
        }

        var state = stateNode.GetString() switch
        {
            "paid" => WorkOrderPaymentState.Paid,
            "revoked" => WorkOrderPaymentState.Revoked,
            "no_payment_required" => WorkOrderPaymentState.NoPaymentRequired,
            _ => throw new JsonException("Unsupported billing payment state.")
        };
        return new ReceiveBillingResultCommand(workOrderId, estimateId, chargeId, sagaId, amount,
            BillingChargeContract.Currency, occurredAt.UtcDateTime, null, revision, state);
    }
}
