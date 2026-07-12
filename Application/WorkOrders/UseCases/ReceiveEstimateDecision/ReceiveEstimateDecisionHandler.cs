using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Domain.WorkOrders.ValueObjects;
using GarageFlow.SharedKernel.Domain.Exceptions;
using Mediator;

namespace GarageFlow.Application.WorkOrders.UseCases.ReceiveEstimateDecision;

public sealed class ReceiveEstimateDecisionHandler(
    IEstimateDecisionInbox inbox,
    IWorkOrderRepository workOrderRepository,
    EstimateDecisionProcessor processor,
    TimeProvider timeProvider)
    : IRequestHandler<ReceiveEstimateDecisionCommand, ReceiveEstimateDecisionResult>
{
    private const string ApprovedDecision = "Approved";
    private const string RejectedDecision = "Rejected";
    private const int Sha256HexLength = 64;

    private readonly IEstimateDecisionInbox _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
    private readonly IWorkOrderRepository _workOrderRepository = workOrderRepository
        ?? throw new ArgumentNullException(nameof(workOrderRepository));
    private readonly EstimateDecisionProcessor _processor = processor
        ?? throw new ArgumentNullException(nameof(processor));
    private readonly TimeProvider _timeProvider = timeProvider
        ?? throw new ArgumentNullException(nameof(timeProvider));

    public async ValueTask<ReceiveEstimateDecisionResult> Handle(
        ReceiveEstimateDecisionCommand request,
        CancellationToken cancellationToken)
    {
        var input = Validate(request);
        var registration = await _inbox.RegisterAsync(
            request.EventId,
            request.PayloadHash,
            request.OccurredAt,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        if (!registration.IsNew)
        {
            if (string.Equals(registration.StoredPayloadHash, request.PayloadHash, StringComparison.Ordinal))
            {
                return new ReceiveEstimateDecisionResult(IsDuplicate: true);
            }

            throw new BusinessRuleViolationException(
                $"Event ID '{request.EventId}' was already used with a different payload.");
        }

        var workOrder = await _workOrderRepository.GetByIdForEstimateMutationAsync(
            input.WorkOrderId,
            cancellationToken);
        if (workOrder is null)
        {
            throw new NotFoundException($"Work order with ID '{request.WorkOrderId}' was not found.");
        }

        if (input.Decision == ApprovedDecision)
        {
            _processor.Approve(workOrder, input.EstimateId);
        }
        else
        {
            await _processor.RejectAsync(workOrder, input.EstimateId, cancellationToken);
        }

        return new ReceiveEstimateDecisionResult(IsDuplicate: false);
    }

    private static ValidatedInput Validate(ReceiveEstimateDecisionCommand request)
    {
        if (request.EventId == Guid.Empty)
        {
            throw new ValidationException("Event identifier cannot be empty.");
        }

        var workOrderId = WorkOrderId.From(request.WorkOrderId);
        var estimateId = EstimateId.From(request.EstimateId);
        var decision = CanonicalizeDecision(request.Decision);

        if (request.OccurredAt.Kind != DateTimeKind.Utc)
        {
            throw new ValidationException("Occurred-at timestamp must be UTC.");
        }

        if (!IsLowercaseSha256Hash(request.PayloadHash))
        {
            throw new ValidationException(
                "Payload hash must be a 64-character lowercase hexadecimal SHA-256 hash.");
        }

        return new ValidatedInput(workOrderId, estimateId, decision);
    }

    private static string CanonicalizeDecision(string decision)
    {
        if (string.Equals(decision, ApprovedDecision, StringComparison.OrdinalIgnoreCase))
        {
            return ApprovedDecision;
        }

        if (string.Equals(decision, RejectedDecision, StringComparison.OrdinalIgnoreCase))
        {
            return RejectedDecision;
        }

        throw new ValidationException("Decision must be either 'Approved' or 'Rejected'.");
    }

    private static bool IsLowercaseSha256Hash(string payloadHash) =>
        payloadHash is { Length: Sha256HexLength }
        && payloadHash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record ValidatedInput(
        WorkOrderId WorkOrderId,
        EstimateId EstimateId,
        string Decision);
}
