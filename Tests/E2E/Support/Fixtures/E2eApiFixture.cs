using System.Diagnostics.CodeAnalysis;
using GarageFlow.Tests.E2E.Support.Factories;
using GarageFlow.Tests.E2E.Support.Helpers;
using Testcontainers.PostgreSql;

namespace GarageFlow.Tests.E2E.Support.Fixtures;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "xUnit disposes fixtures through IAsyncLifetime.DisposeAsync.")]
public sealed class E2eApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("garageflow_e2e")
        .WithUsername("garageflow")
        .WithPassword("garageflow")
        .Build();

    private E2eWebApplicationFactory? _factory;

    public string DatabaseConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new E2eWebApplicationFactory(_postgres.GetConnectionString());
    }

    public HttpClient CreateClient()
    {
        return RequiredFactory().CreateClient();
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        try
        {
            await client.AuthenticateAsActiveBootstrapAdminAsync();
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

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

    private E2eWebApplicationFactory RequiredFactory()
    {
        return _factory ?? throw new InvalidOperationException("E2E API factory has not been initialized.");
    }
}
