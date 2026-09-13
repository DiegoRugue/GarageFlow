using System.Collections.Concurrent;
using System.Diagnostics;
using GarageFlow.Tests.Integration.Support.Factories;
using GarageFlow.Tests.Integration.Support.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using OpenTelemetry.Resources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace GarageFlow.Tests.Integration.Host;

[Collection(WebApplicationFactoryStartupTestGroup.Name)]
public sealed class ObservabilityTests
{
    [Theory]
    [InlineData("http://private:password@localhost:4318")]
    [InlineData("http://localhost:4318/?password=secret")]
    [InlineData("ftp://localhost")]
    [InlineData("not-a-url")]
    public void EnabledTelemetryRejectsInvalidEndpoint(string endpoint)
    {
        using var factory = new GarageFlowWebApplicationFactory(Guid.NewGuid().ToString(), additionalSettings: Settings(true, endpoint));
        using var configured = factory.WithWebHostBuilder(_ => { });
        Assert.Throws<InvalidOperationException>(() => configured.CreateClient());
    }

    [Fact]
    public void DisabledTelemetryDoesNotRegisterExportersOrValidateUnusedEndpoint()
    {
        using var factory = new GarageFlowWebApplicationFactory(Guid.NewGuid().ToString(), additionalSettings: Settings(false, "unused"));
        using var configured = factory.WithWebHostBuilder(_ => { });
        using var client = configured.CreateClient();
        Assert.Null(configured.Services.GetService<TracerProvider>());
        Assert.Null(configured.Services.GetService<MeterProvider>());
    }

    [Fact]
    public async Task HttpTelemetryCorrelatesSafeLogsTracesAndRouteMetricsDuringCollectorOutage()
    {
        var traces = new TraceCapture();
        var logs = new LogCapture();
        var metrics = new MetricCapture();
        using var factory = new GarageFlowWebApplicationFactory(Guid.NewGuid().ToString(), additionalSettings: Settings(true, "http://127.0.0.1:1"));
        using var configured = factory.WithWebHostBuilder(builder =>
        {

            builder.ConfigureServices(services =>
            {
                services.ConfigureOpenTelemetryTracerProvider(provider => provider.AddProcessor(new SimpleActivityExportProcessor(traces)));
                services.ConfigureOpenTelemetryMeterProvider(provider => provider.AddReader(new BaseExportingMetricReader(metrics)));
                services.Configure<OpenTelemetryLoggerOptions>(options => options.AddProcessor(new SimpleLogRecordExportProcessor(logs)));
            });
        });
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer jwt-private-marker");
        client.DefaultRequestHeaders.Add("traceparent", "00-11111111111111111111111111111111-2222222222222222-01");
        client.DefaultRequestHeaders.Add("tracestate", "vendor=state-private-marker");
        client.DefaultRequestHeaders.Add("baggage", "cpf=cpf-private-marker");
        client.DefaultRequestHeaders.Add("User-Agent", "agent-private-marker");
        var timer = Stopwatch.StartNew();
        using var response = await client.GetAsync("/health?password=password-private-marker");
        response.EnsureSuccessStatusCode();
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5));
        using var unmatched = await client.PostAsync("/path-private-marker", new StringContent("body-private-marker"));
        using var failure = await client.GetAsync("/integration-tests/throw/unhandled?cpf=cpf-private-marker");
        Assert.Equal(500, (int)failure.StatusCode);
        configured.Services.GetRequiredService<MeterProvider>().ForceFlush();
        var health = Assert.Single(traces.Items, item => item.Name == "GET /health");
        Assert.Contains(logs.Items, item => item.TraceId == health.TraceId && item.SpanId == health.SpanId);
        Assert.Contains(metrics.Items, item => item.Contains("http.server.request.duration") && item.Contains("/health"));
        Assert.Contains(traces.Items, item => item.Status == ActivityStatusCode.Error);
        var serialized = string.Join("\n", traces.Items.Select(item => item.Data).Concat(logs.Items.Select(item => item.Data)).Concat(metrics.Items));
        foreach (var secret in new[] { "state-private-marker", "jwt-private-marker", "cpf-private-marker", "password-private-marker", "path-private-marker", "body-private-marker", "agent-private-marker", "Integration test exception." })
            Assert.DoesNotContain(secret, serialized);
    }

    [Fact]
    public async Task OtlpWirePayloadsUseSameResourceAndContainNoSensitiveRequestData()
    {
        var payloads = new ConcurrentBag<(string Path, string Body)>();
        var logReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            ActivityStopped = activity =>
            {
                if (activity.GetTagItem("http.route")?.ToString() == "/health") serverStopped.TrySetResult();
            }
        };
        ActivitySource.AddActivityListener(listener);
        var collectorBuilder = Microsoft.AspNetCore.Builder.WebApplication.CreateSlimBuilder();
        collectorBuilder.Logging.ClearProviders();
        collectorBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var collector = collectorBuilder.Build();
        collector.MapPost("/v1/{signal}", async (Microsoft.AspNetCore.Http.HttpContext context) =>
        {
            using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body);
            payloads.Add((context.Request.Path.Value!, System.Text.Encoding.UTF8.GetString(body.ToArray())));
            if (context.Request.Path == "/v1/logs") logReceived.TrySetResult();
            context.Response.ContentType = "application/x-protobuf";
        });
        await collector.StartAsync();
        using var factory = new GarageFlowWebApplicationFactory(Guid.NewGuid().ToString(), additionalSettings: Settings(true, collector.Urls.Single()));
        using var client = factory.CreateClient();
        using var networkClient = new HttpClient();
        using var dependencyResponse = await networkClient.GetAsync(collector.Urls.Single() + "/dependency-private-marker?password=wire-private-marker");
        client.DefaultRequestHeaders.Add("traceparent", "00-33333333333333333333333333333333-4444444444444444-01");
        using var response = await client.GetAsync("/health?password=wire-private-marker");
        response.EnsureSuccessStatusCode();
        await serverStopped.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(factory.Services.GetRequiredService<TracerProvider>().ForceFlush());
        Assert.True(factory.Services.GetRequiredService<MeterProvider>().ForceFlush());
        await logReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        foreach (var signal in new[] { "/v1/traces", "/v1/metrics", "/v1/logs" })
        {
            var payload = Assert.Single(payloads, item => item.Path == signal);
            Assert.Contains("garageflow-api", payload.Body);
            Assert.Contains("deployment.environment.name", payload.Body);
            Assert.Contains("integration", payload.Body);
            Assert.DoesNotContain("wire-private-marker", payload.Body);
            Assert.DoesNotContain("dependency-private-marker", payload.Body);
        }
        Assert.Contains("/health", Assert.Single(payloads, item => item.Path == "/v1/traces").Body);
        var traceResource = factory.Services.GetRequiredService<TracerProvider>().GetResource();
        var metricResource = factory.Services.GetRequiredService<MeterProvider>().GetResource();
        var instance = Assert.Single(traceResource.Attributes, item => item.Key == "service.instance.id").Value;
        Assert.Equal(instance, Assert.Single(metricResource.Attributes, item => item.Key == "service.instance.id").Value);
        Assert.All(payloads, payload => Assert.Contains(instance.ToString()!, payload.Body));
        await collector.StopAsync();
    }
    [Fact]
    public async Task JsonConsoleEmitsOnlySafeCompletionFieldsAndTraceCorrelation()
    {
        var original = Console.Out;
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(output);
            using (var factory = new GarageFlowWebApplicationFactory(Guid.NewGuid().ToString(), additionalSettings: Settings(true, "http://127.0.0.1:1")))
            using (var client = factory.CreateClient())
            using (var response = await client.GetAsync("/health?password=console-private-marker"))
            {
                response.EnsureSuccessStatusCode();
            }
        }
        finally
        {
            Console.SetOut(original);
        }
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var line = Assert.Single(lines);
        using var json = System.Text.Json.JsonDocument.Parse(line);
        Assert.Equal("GarageFlow.Host.Observability.RequestTelemetryMiddleware", json.RootElement.GetProperty("Category").GetString());
        var state = json.RootElement.GetProperty("State");
        Assert.Equal("/health", state.GetProperty("Route").GetString());
        Assert.Equal(32, state.GetProperty("TraceId").GetString()!.Length);
        Assert.Equal(16, state.GetProperty("SpanId").GetString()!.Length);
        Assert.DoesNotContain("console-private-marker", line);
    }
    [Fact]
    public void ConsoleQueueDropsTelemetryInsteadOfBlockingRequestsWhenFull()
    {
        using var factory = new GarageFlowWebApplicationFactory(Guid.NewGuid().ToString(), additionalSettings: Settings(true, "http://127.0.0.1:1"));
        using var client = factory.CreateClient();
        var options = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.Extensions.Logging.Console.ConsoleLoggerOptions>>().Value;
        Assert.Equal(Microsoft.Extensions.Logging.Console.ConsoleLoggerQueueFullMode.DropWrite, options.QueueFullMode);
        Assert.Equal(2048, options.MaxQueueLength);
    }
    private static Dictionary<string, string?> Settings(bool enabled, string endpoint) => new()
        {
            ["Observability:Enabled"] = enabled.ToString(),
            ["Observability:OtlpEndpoint"] = endpoint,
            ["Observability:Environment"] = "integration"
        };

    private sealed class TraceCapture : BaseExporter<Activity>
    {
        public ConcurrentBag<(string Name, ActivityTraceId TraceId, ActivitySpanId SpanId, ActivityStatusCode Status, string Data)> Items { get; } = [];
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var item in batch)
                Items.Add((item.DisplayName, item.TraceId, item.SpanId, item.Status, item.DisplayName + item.StatusDescription + item.TraceStateString + string.Join(";", item.TagObjects) + string.Join(";", item.Baggage) + string.Join(";", item.Events.Select(e => e.Name + string.Join(";", e.Tags)))));
            return ExportResult.Success;
        }
    }

    private sealed class LogCapture : BaseExporter<LogRecord>
    {
        public ConcurrentBag<(ActivityTraceId TraceId, ActivitySpanId SpanId, string Data)> Items { get; } = [];
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var item in batch)
                Items.Add((item.TraceId, item.SpanId, item.Body + item.FormattedMessage + item.Exception + string.Join(";", item.Attributes ?? [])));
            return ExportResult.Success;
        }
    }

    private sealed class MetricCapture : BaseExporter<Metric>
    {
        public ConcurrentBag<string> Items { get; } = [];
        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var item in batch)
                foreach (ref readonly var point in item.GetMetricPoints())
                {
                    var tags = new List<string>();
                    foreach (var tag in point.Tags) tags.Add($"{tag.Key}={tag.Value}");
                    Items.Add(item.Name + string.Join(";", tags));
                }
            return ExportResult.Success;
        }
    }
}
