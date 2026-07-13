using System.Collections.Concurrent;
using GarageFlow.Tests.Integration.Support.Factories;
using GarageFlow.Tests.Integration.Support.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace GarageFlow.Tests.Integration.Support.Fixtures;

public sealed class GarageFlowApiFixture : IAsyncLifetime
{
    private readonly ConcurrentBag<GarageFlowWebApplicationFactory> _factories = [];
    private readonly string _databaseName = $"garageflow-tests-{Guid.NewGuid():N}";

    public HttpClient CreateClient(bool disableAutoMigrate = false)
    {
        var factory = new GarageFlowWebApplicationFactory(_databaseName, disableAutoMigrate);

        _factories.Add(factory);
        return factory.CreateClient();
    }

    public HttpClient CreateClientWithScope(out IServiceScope scope)
    {
        var factory = new GarageFlowWebApplicationFactory(_databaseName);
        _factories.Add(factory);
        var client = factory.CreateClient();
        scope = factory.Services.CreateScope();
        return client;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(bool disableAutoMigrate = false)
    {
        var client = CreateClient(disableAutoMigrate);
        await client.AuthenticateAsActiveBootstrapAdminAsync();
        return client;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        while (_factories.TryTake(out var factory))
        {
            await factory.DisposeAsync();
        }
    }
}
