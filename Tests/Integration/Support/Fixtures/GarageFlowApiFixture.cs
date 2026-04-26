using System.Collections.Concurrent;
using GarageFlow.Tests.Integration.Support.Factories;

namespace GarageFlow.Tests.Integration.Support.Fixtures;

public sealed class GarageFlowApiFixture : IAsyncLifetime
{
    private readonly ConcurrentBag<GarageFlowWebApplicationFactory> _factories = [];
    private int _databaseSequence;

    public HttpClient CreateClient(bool disableAutoMigrate = false)
    {
        var databaseName = $"garageflow-tests-{Interlocked.Increment(ref _databaseSequence):D4}-{Guid.NewGuid():N}";
        var factory = new GarageFlowWebApplicationFactory(databaseName, disableAutoMigrate);

        _factories.Add(factory);
        return factory.CreateClient();
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
