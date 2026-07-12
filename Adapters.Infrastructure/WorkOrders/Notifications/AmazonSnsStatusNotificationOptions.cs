namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Notifications;

public sealed class AmazonSnsStatusNotificationOptions
{
    public const string SectionName = "Integrations:Sns";
    public const string RequiredRegion = "us-east-1";

    public string Region { get; init; } = RequiredRegion;

    public string TopicArn { get; init; } = string.Empty;
}
