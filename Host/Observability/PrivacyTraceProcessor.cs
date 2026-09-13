using System.Diagnostics;
using OpenTelemetry;

namespace GarageFlow.Host.Observability;

public sealed class PrivacyTraceProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> AllowedTags = ["http.request.method", "http.response.status_code", "http.route", "network.protocol.version", "url.scheme"];

    public override void OnEnd(Activity activity)
    {
        foreach (var tag in activity.TagObjects.ToArray())
            if (!AllowedTags.Contains(tag.Key)) activity.SetTag(tag.Key, null);
        foreach (var baggage in activity.Baggage.ToArray()) activity.SetBaggage(baggage.Key, null);
        activity.TraceStateString = null;
        activity.SetStatus(activity.Status);
        if (activity.Status == ActivityStatusCode.Error) activity.SetTag("error.type", "request_failed");
        activity.DisplayName = activity.Kind == ActivityKind.Server
            ? $"{RequestTelemetryMiddleware.SafeMethod(activity.GetTagItem("http.request.method")?.ToString())} {activity.GetTagItem("http.route") ?? "unmatched"}"
            : "HTTP request";
    }
}
