using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Vehicles.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Shared.Vehicles;

namespace GarageFlow.Tests.Integration.Support.Factories;

[Collection(WebApplicationFactoryStartupTestGroup.Name)]
public sealed class GarageFlowWebApplicationFactoryTests
{
    [Fact]
    public async Task DistinctFactories_ShouldUseIsolatedInMemoryDatabases()
    {
        using var firstFactory = new GarageFlowWebApplicationFactory($"factory-first-{Guid.NewGuid():N}");
        using var secondFactory = new GarageFlowWebApplicationFactory($"factory-second-{Guid.NewGuid():N}");
        using var firstClient = firstFactory.CreateClient();
        using var secondClient = secondFactory.CreateClient();
        await firstClient.AuthenticateAsActiveBootstrapAdminAsync();
        await secondClient.AuthenticateAsActiveBootstrapAdminAsync();
        var request = new VehicleColorBuilder()
            .WithName($"Shared-name-{Guid.NewGuid():N}")
            .BuildCreateRequest();

        using var firstResponse = await firstClient.PostAsJsonAsync("/vehicle-colors", request);
        using var secondResponse = await secondClient.PostAsJsonAsync("/vehicle-colors", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
    }
}
