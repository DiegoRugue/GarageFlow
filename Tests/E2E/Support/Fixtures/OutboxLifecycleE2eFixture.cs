using System.Diagnostics.CodeAnalysis;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Tests.E2E.Support.Fakes;
using GarageFlow.Tests.E2E.Support.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace GarageFlow.Tests.E2E.Support.Fixtures;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "xUnit disposes fixtures through IAsyncLifetime.DisposeAsync.")]
public sealed class OutboxLifecycleE2eFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("garageflow_outbox_e2e")
        .WithUsername("garageflow")
        .WithPassword("garageflow")
        .Build();
    private E2eWebApplicationFactory? _factory;

    public RecordingWorkOrderStatusNotificationPublisher Publisher { get; } = new();
    public string DatabaseConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new E2eWebApplicationFactory(_postgres.GetConnectionString(), services =>
        {
            services.RemoveAll<IOptions<IntegrationOutboxOptions>>();
            services.AddSingleton(Options.Create(new IntegrationOutboxOptions
            {
                Enabled = true,
                BatchSize = 2,
                PollingIntervalSeconds = 1,
                LeaseDurationSeconds = 2,
                InitialRetryDelaySeconds = 1,
                MaxRetryDelaySeconds = 2
            }));
            services.AddSingleton(Publisher);
            services.AddScoped<IWorkOrderStatusNotificationPublisher>(provider =>
                provider.GetRequiredService<RecordingWorkOrderStatusNotificationPublisher>());
            services.AddScoped<IIntegrationOutboxProcessor, IntegrationOutboxProcessor>();
            services.AddHostedService<IntegrationOutboxPublisher>();
        });
        using var client = _factory.CreateClient();
    }

    public IServiceScope CreateScope() => RequiredFactory().Services.CreateScope();

    public async Task DisposeAsync()
    {
        try
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
            }
        }
        finally
        {
            await _postgres.DisposeAsync();
        }
    }

    private E2eWebApplicationFactory RequiredFactory() =>
        _factory ?? throw new InvalidOperationException("Outbox lifecycle factory has not been initialized.");
}
