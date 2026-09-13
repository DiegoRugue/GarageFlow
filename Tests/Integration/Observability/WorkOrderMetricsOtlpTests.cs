using System.Collections.Concurrent;
using System.Text;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.WorkOrders.ReadModels;
using GarageFlow.Tests.Integration.Support.Factories;
using GarageFlow.Tests.Integration.Support.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Metrics;

namespace GarageFlow.Tests.Integration.Observability;

[Collection(WebApplicationFactoryStartupTestGroup.Name)]
public sealed class WorkOrderMetricsOtlpTests
{
    [Fact]
    public async Task OtlpExportsDailyGaugesWithResourceAndStopsExportingExpiredValues()
    {
        var payloads = new ConcurrentQueue<string>();
        var collectorBuilder = WebApplication.CreateSlimBuilder();
        collectorBuilder.Logging.ClearProviders();
        collectorBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var collector = collectorBuilder.Build();
        collector.MapPost("/v1/{signal}", async (HttpContext context, string signal) =>
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer);
            if (signal == "metrics") payloads.Enqueue(Encoding.UTF8.GetString(buffer.ToArray()));
            context.Response.ContentType = "application/x-protobuf";
        });
        await collector.StartAsync();
        var clock = new ManualMetricsTimeProvider(new DateTimeOffset(2026, 9, 13, 1, 0, 0, TimeSpan.Zero));
        var fail = false;
        var queries = new Mock<IWorkOrderMetricsQueries>();
        queries.Setup(q => q.GetDailyAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(() => fail
                ? Task.FromException<WorkOrderDailyMetricsReadModel>(new InvalidOperationException("customer-private-marker"))
                : Task.FromResult(new WorkOrderDailyMetricsReadModel(12, 2, 1200)));
        using var factory = new GarageFlowWebApplicationFactory(Guid.NewGuid().ToString(), additionalSettings: new Dictionary<string, string?>
        {
            ["Observability:Enabled"] = "true",
            ["Observability:Environment"] = "integration",
            ["Observability:OtlpEndpoint"] = collector.Urls.Single()
        });
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            services.RemoveAll<IWorkOrderMetricsQueries>();
            services.AddScoped(_ => queries.Object);
        }));
        using var client = configured.CreateClient();
        await WaitForPollingAsync(clock);
        var provider = configured.Services.GetRequiredService<MeterProvider>();
        Assert.True(provider.ForceFlush());
        var fresh = string.Join("\n", payloads);
        foreach (var name in new[] { "garageflow.work_orders.created", "garageflow.work_orders.completed", "garageflow.work_orders.duration.mean", "garageflow.work_orders.snapshot.timestamp" })
            Assert.Contains(name, fresh);
        foreach (var attribute in new[] { "work_orders.date", "work_orders.timezone", "America/Sao_Paulo", "2026-09-12", "2026-09-06", "service.instance.id", "garageflow-api", "deployment.environment.name", "integration" })
            Assert.Contains(attribute, fresh);
        while (payloads.TryDequeue(out _)) { }
        fail = true;
        clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        await WaitForPollingAsync(clock);
        Assert.True(provider.ForceFlush());
        Assert.NotEmpty(payloads);
        var stale = string.Join("\n", payloads);
        Assert.DoesNotContain("garageflow.work_orders.", stale);
        Assert.DoesNotContain("customer-private-marker", stale);
        await collector.StopAsync();
    }

    private static async Task WaitForPollingAsync(ManualMetricsTimeProvider clock)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!clock.HasTimer(TimeSpan.FromMinutes(5))) await Task.Delay(10, timeout.Token);
    }
}
