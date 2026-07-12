using Microsoft.Extensions.Options;

namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Notifications;

public sealed class AmazonSnsStatusNotificationOptionsValidator
    : IValidateOptions<AmazonSnsStatusNotificationOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        AmazonSnsStatusNotificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.Equals(
                options.Region,
                AmazonSnsStatusNotificationOptions.RequiredRegion,
                StringComparison.Ordinal) ||
            !IsValidTopicArn(options.TopicArn))
        {
            return ValidateOptionsResult.Fail(
                $"{AmazonSnsStatusNotificationOptions.SectionName} requires Region " +
                $"'{AmazonSnsStatusNotificationOptions.RequiredRegion}' and a non-placeholder SNS topic ARN " +
                "for that region.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsValidTopicArn(string? topicArn)
    {
        if (string.IsNullOrWhiteSpace(topicArn))
        {
            return false;
        }

        var parts = topicArn.Split(':');
        if (parts.Length != 6 ||
            !string.Equals(parts[0], "arn", StringComparison.Ordinal) ||
            !string.Equals(parts[1], "aws", StringComparison.Ordinal) ||
            !string.Equals(parts[2], "sns", StringComparison.Ordinal) ||
            !string.Equals(
                parts[3],
                AmazonSnsStatusNotificationOptions.RequiredRegion,
                StringComparison.Ordinal) ||
            parts[4].Length != 12 ||
            !parts[4].All(char.IsAsciiDigit) ||
            string.IsNullOrWhiteSpace(parts[5]) ||
            parts[5].Length > 256 ||
            !parts[5].All(IsValidTopicNameCharacter))
        {
            return false;
        }

        var normalizedTopicName = parts[5]
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return !normalizedTopicName.Contains("placeholder", StringComparison.Ordinal) &&
               !normalizedTopicName.Contains("replace", StringComparison.Ordinal) &&
               !normalizedTopicName.Contains("changeme", StringComparison.Ordinal) &&
               !normalizedTopicName.Contains("setme", StringComparison.Ordinal) &&
               !normalizedTopicName.Contains("configure", StringComparison.Ordinal) &&
               !normalizedTopicName.Contains("todo", StringComparison.Ordinal);
    }

    private static bool IsValidTopicNameCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '_' or '-';
}
