using System.Diagnostics;

namespace GarageFlow.Host.Observability;

public sealed partial class RequestTelemetryMiddleware(RequestDelegate next, ILogger<RequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var start = Stopwatch.GetTimestamp();
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
        var method = SafeMethod(context.Request.Method);
        try
        {
            await next(context);
        }
        finally
        {
            var activity = Activity.Current;
            if (context.Response.StatusCode >= 500) activity?.SetStatus(ActivityStatusCode.Error);
            if (logger.IsEnabled(LogLevel.Information))
            {
                var durationMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                var traceId = activity?.TraceId.ToHexString();
                var spanId = activity?.SpanId.ToHexString();
                LogRequest(logger, method, route, context.Response.StatusCode, durationMs, traceId, spanId);
            }
        }
    }

    [LoggerMessage(SkipEnabledCheck = true, Level = LogLevel.Information, Message = "HTTP {Method} {Route} completed {StatusCode} in {DurationMs} ms; trace {TraceId} span {SpanId}")]
    private static partial void LogRequest(ILogger logger, string method, string route, int statusCode, double durationMs, string? traceId, string? spanId);

    public static string SafeMethod(string? method) => method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" or "CONNECT" or "TRACE" ? method : "_OTHER";
}
