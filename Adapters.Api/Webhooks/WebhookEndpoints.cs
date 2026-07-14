using GarageFlow.Adapters.Api.Webhooks.EstimateDecisions;

namespace GarageFlow.Adapters.Api.Webhooks;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapEstimateDecisionWebhookEndpoint();
        return app;
    }
}
