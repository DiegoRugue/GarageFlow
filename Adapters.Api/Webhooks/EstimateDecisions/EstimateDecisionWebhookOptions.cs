namespace GarageFlow.Adapters.Api.Webhooks.EstimateDecisions;

public sealed class EstimateDecisionWebhookOptions
{
    public const string SectionName = "Webhooks:EstimateDecisions";
    public const int AllowedClockSkewSeconds = 300;
    public const int MaximumBodySizeBytes = 65_536;

    public string HmacSecret { get; set; } = string.Empty;
}
