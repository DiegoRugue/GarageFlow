using System.Security.Cryptography;
using System.Text.Json;
using GarageFlow.Application.WorkOrders.UseCases.ReceiveEstimateDecision;
using Mediator;

namespace GarageFlow.Adapters.Api.Webhooks.EstimateDecisions;

public static class EstimateDecisionWebhookEndpoint
{
    private const string InvalidSignatureMessage = "Webhook signature is invalid or expired.";

    public static IEndpointRouteBuilder MapEstimateDecisionWebhookEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/estimate-decisions", HandleAsync)
            .WithName("ReceiveEstimateDecisionWebhook")
            .WithTags("Webhooks")
            .WithSummary("Receive a signed estimate decision event")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        EstimateDecisionWebhookSignatureValidator signatureValidator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var rawBody = await ReadBodyAsync(request.Body, cancellationToken);
        var timestamp = request.Headers["X-GarageFlow-Timestamp"].FirstOrDefault();
        var signature = request.Headers["X-GarageFlow-Signature"].FirstOrDefault();
        if (!signatureValidator.IsValid(timestamp, signature, rawBody))
        {
            throw new UnauthorizedAccessException(InvalidSignatureMessage);
        }

        EstimateDecisionWebhookRequest? payload;
        try
        {
            payload = JsonSerializer.Deserialize<EstimateDecisionWebhookRequest>(
                rawBody,
                JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return Results.Problem(
                title: "Validation error",
                detail: "Webhook payload is malformed.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (payload is null)
        {
            return Results.Problem(
                title: "Validation error",
                detail: "Webhook payload is malformed.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var payloadHash = Convert.ToHexStringLower(SHA256.HashData(rawBody));
        await mediator.Send(
            new ReceiveEstimateDecisionCommand(
                payload.EventId,
                payload.WorkOrderId,
                payload.EstimateId,
                payload.Decision,
                payload.OccurredAt,
                payloadHash),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<byte[]> ReadBodyAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffer = new byte[EstimateDecisionWebhookOptions.MaximumBodySizeBytes + 1];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await body.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return buffer.AsSpan(0, totalRead).ToArray();
    }
}
