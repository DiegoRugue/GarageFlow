namespace GarageFlow.Adapters.Infrastructure.WorkOrders.Execution;

internal sealed record ExecutionSchedulingQueueOptions
{
    public const string SectionName = "Integrations:ExecutionScheduling";

    public bool Enabled { get; init; }
    public string CommandQueueUrl { get; init; } = string.Empty;
    public string ResultQueueUrl { get; init; } = string.Empty;
    public string? BillingCommandQueueUrl { get; init; }
    public string? BillingResultQueueUrl { get; init; }

    public bool HasValidQueues() =>
        IsStandardQueueUrl(CommandQueueUrl)
        && IsStandardQueueUrl(ResultQueueUrl)
        && !string.Equals(CommandQueueUrl, ResultQueueUrl, StringComparison.Ordinal)
        && (BillingCommandQueueUrl is null ||
            (IsStandardQueueUrl(BillingCommandQueueUrl)
             && !string.Equals(BillingCommandQueueUrl, CommandQueueUrl, StringComparison.Ordinal)
             && !string.Equals(BillingCommandQueueUrl, ResultQueueUrl, StringComparison.Ordinal)))
        && (BillingResultQueueUrl is null ||
            (IsStandardQueueUrl(BillingResultQueueUrl)
             && !string.Equals(BillingResultQueueUrl, CommandQueueUrl, StringComparison.Ordinal)
             && !string.Equals(BillingResultQueueUrl, ResultQueueUrl, StringComparison.Ordinal)
             && !string.Equals(BillingResultQueueUrl, BillingCommandQueueUrl, StringComparison.Ordinal)));

    private static bool IsStandardQueueUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        && uri.AbsolutePath.Length > 1
        && !uri.AbsolutePath.EndsWith(".fifo", StringComparison.Ordinal)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
