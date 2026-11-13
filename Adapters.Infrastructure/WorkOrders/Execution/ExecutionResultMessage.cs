using System.Text.Json;
using GarageFlow.Application.Common.Messaging;
using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.UseCases.ReceiveExecutionProgress;
using GarageFlow.Application.WorkOrders.UseCases.ReceiveExecutionScheduleDecision;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Execution;

internal sealed record ExecutionResultMessage(
    string SchemaVersion,
    string MessageType,
    string MessageId,
    string CorrelationId,
    string OccurredAt,
    string WorkOrderId,
    string EstimateId,
    string ExecutionId,
    JsonElement Payload)
{
    public static ICommand ReadCommand(string body)
    {
        var result = JsonSerializer.Deserialize<ExecutionResultMessage>(body, WorkOrderResultJson.Options)
            ?? throw new JsonException("An execution decision is required.");
        if (result.SchemaVersion != ExecutionSchedulingContract.SchemaVersion
            || result.Payload.ValueKind != JsonValueKind.Object
            || !WorkOrderResultJson.TryReadTimestamp(result.OccurredAt, out var occurredAt))
        {
            throw new JsonException("Invalid execution decision envelope.");
        }

        _ = WorkOrderResultJson.ReadId(result.MessageId);
        var workOrderId = WorkOrderResultJson.ReadId(result.WorkOrderId);
        var estimateId = WorkOrderResultJson.ReadId(result.EstimateId);
        var executionId = WorkOrderResultJson.ReadId(result.ExecutionId);
        var sagaId = WorkOrderResultJson.ReadId(result.CorrelationId);
        return result.MessageType switch
        {
            ExecutionSchedulingContract.AcceptedKey when result.Payload.GetPropertyCount() == 0 =>
                new ReceiveExecutionScheduleDecisionCommand(workOrderId, estimateId, executionId, sagaId, Accepted: true),
            ExecutionSchedulingContract.RejectedKey when IsKnownRejection(result.Payload) =>
                new ReceiveExecutionScheduleDecisionCommand(workOrderId, estimateId, executionId, sagaId, Accepted: false),
            ExecutionSchedulingContract.StartedKey when result.Payload.GetPropertyCount() == 0 =>
                new ReceiveExecutionProgressCommand(workOrderId, estimateId, executionId, sagaId, occurredAt.UtcDateTime, Completed: false),
            ExecutionSchedulingContract.CompletedKey when result.Payload.GetPropertyCount() == 0 =>
                new ReceiveExecutionProgressCommand(workOrderId, estimateId, executionId, sagaId, occurredAt.UtcDateTime, Completed: true),
            _ => throw new JsonException("Unsupported execution result.")
        };
    }

    private static bool IsKnownRejection(JsonElement payload) =>
        payload.GetPropertyCount() == 1
        && payload.TryGetProperty("reasonCode", out var reason)
        && reason.ValueKind == JsonValueKind.String
        && reason.ValueEquals(ExecutionSchedulingContract.AlreadyScheduledReason);

}
