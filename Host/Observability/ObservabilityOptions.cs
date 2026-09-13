namespace GarageFlow.Host.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";
    public bool Enabled { get; set; }
#pragma warning disable S5332 // ADR 0002: private in-cluster OTLP receiver; the collector owns TLS to New Relic.
    public string OtlpEndpoint { get; set; } = "http://garageflow-otel.newrelic.svc.cluster.local:4318";
#pragma warning restore S5332
    public string? Environment { get; set; }

    public Uri ValidateEndpoint()
    {
        if (!Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
        {
            throw new InvalidOperationException("Observability:OtlpEndpoint must be an HTTP(S) base URL without credentials, path, query or fragment.");
        }
        return uri;
    }
}
