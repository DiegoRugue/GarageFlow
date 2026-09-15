using OpenTelemetry;
using Microsoft.Extensions.Logging.Console;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;

namespace GarageFlow.Host.Observability;

public static class ObservabilityExtensions
{
    private const int ExportTimeoutMilliseconds = 2000;
    private const int MaxQueueSize = 2048;
    private const int MaxExportBatchSize = 512;
    private const int BatchDelayMilliseconds = 5000;
    private const int MetricIntervalMilliseconds = 30000;

    public static bool AddGarageFlowObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = builder.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new();
        if (!options.Enabled) return false;
        var endpoint = options.ValidateEndpoint();
        builder.Services.AddHostedService<WorkOrderMetricsPublisher>();
        var environment = options.Environment ?? builder.Environment.EnvironmentName;
        if (string.IsNullOrWhiteSpace(environment) || environment.Length > 64 || environment.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new InvalidOperationException("Observability:Environment must be a short environment name.");
        var instanceId = Guid.NewGuid().ToString();
        ResourceBuilder Resource() => ResourceBuilder.CreateEmpty().AddService("garageflow-api", serviceInstanceId: instanceId)
            .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", environment)]);
        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = false);
        builder.Services.PostConfigure<ConsoleLoggerOptions>(options =>
        {
            options.MaxQueueLength = MaxQueueSize;
            options.QueueFullMode = ConsoleLoggerQueueFullMode.DropWrite;
        });
        // Framework messages and exception objects may contain SQL or user input.
        // Only fixed safe schemas are admitted, even if log levels are overridden.
        builder.Services.PostConfigure<LoggerFilterOptions>(options =>
        {
            options.Rules.Clear();
            options.Rules.Add(new LoggerFilterRule(null, null, LogLevel.Information,
                (_, category, level) =>
                    (category == typeof(RequestTelemetryMiddleware).FullName
                     || category == typeof(IntegrationOutboxTelemetry).FullName)
                    && level >= LogLevel.Information));
        });
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(Resource());
            logging.IncludeScopes = false;
            logging.IncludeFormattedMessage = false;
            logging.AddOtlpExporter((exporter, processor) =>
            {
                ConfigureExporter(exporter, endpoint, "logs");
                processor.ExportProcessorType = ExportProcessorType.Batch;
                processor.BatchExportProcessorOptions = new BatchExportLogRecordProcessorOptions
                {
                    MaxQueueSize = MaxQueueSize,
                    MaxExportBatchSize = MaxExportBatchSize,
                    ScheduledDelayMilliseconds = BatchDelayMilliseconds,
                    ExporterTimeoutMilliseconds = ExportTimeoutMilliseconds
                };
            });
        });
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.Clear().AddService("garageflow-api", serviceInstanceId: instanceId)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", environment)]))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
                .AddAspNetCoreInstrumentation(options => options.RecordException = false)
                .AddHttpClientInstrumentation(options => options.RecordException = false)
                .AddProcessor(new PrivacyTraceProcessor())
                .AddOtlpExporter(exporter => ConfigureExporter(exporter, endpoint, "traces")))
            .WithMetrics(metrics => metrics
                .AddMeter("Microsoft.AspNetCore.Hosting", "System.Net.Http", WorkOrderMetricsPublisher.MeterName, IntegrationOutboxTelemetry.MeterName)
                .AddRuntimeInstrumentation()
                .AddView("http.server.request.duration", new MetricStreamConfiguration { TagKeys = ["http.request.method", "http.route", "http.response.status_code", "error.type"] })
                .AddView("http.client.request.duration", new MetricStreamConfiguration { TagKeys = ["http.request.method", "http.response.status_code", "error.type"] })
                .AddView("garageflow.integration.outbox.results", new MetricStreamConfiguration { TagKeys = ["outbox.outcome", "outbox.failure.kind"] })
                .AddView("garageflow.integration.outbox.polls", new MetricStreamConfiguration { TagKeys = ["outbox.outcome"] })
                .AddView(instrument => instrument.Name.StartsWith("http.", StringComparison.Ordinal) && instrument.Name is not "http.server.request.duration" and not "http.client.request.duration" ? MetricStreamConfiguration.Drop : null)
                .AddOtlpExporter((exporter, reader) =>
                {
                    ConfigureExporter(exporter, endpoint, "metrics");
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = MetricIntervalMilliseconds;
                    reader.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = ExportTimeoutMilliseconds;
                }));
        return true;
    }

    private static void ConfigureExporter(OtlpExporterOptions exporter, Uri endpoint, string signal)
    {
        exporter.Endpoint = new Uri(endpoint, $"v1/{signal}");
        exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
        exporter.Headers = null;
        exporter.TimeoutMilliseconds = ExportTimeoutMilliseconds;
        exporter.ExportProcessorType = ExportProcessorType.Batch;
        exporter.BatchExportProcessorOptions = new BatchExportProcessorOptions<System.Diagnostics.Activity>
        {
            MaxQueueSize = MaxQueueSize,
            MaxExportBatchSize = MaxExportBatchSize,
            ScheduledDelayMilliseconds = BatchDelayMilliseconds,
            ExporterTimeoutMilliseconds = ExportTimeoutMilliseconds
        };
    }
}
