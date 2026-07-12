using System.Net;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;

namespace GarageFlow.Tests.E2E.Smoke;

[Collection(E2eApiCollection.Name)]
public sealed class SmokeE2eTests(E2eApiFixture fixture)
{
    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task Health_ShouldReturnOk_WhenApiRunsAgainstPostgres()
    {
        // Given the real API is bootstrapped against PostgreSQL.
        using var client = _fixture.CreateClient();

        // When the public health endpoint is requested.
        using var response = await client.GetAsync("/health");

        // Then the API confirms it is available.
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task BootstrapAdmin_ShouldAuthenticateAndAccessProtectedRoute()
    {
        // Given the bootstrap admin can authenticate against the real API.
        using var client = await _fixture.CreateAuthenticatedClientAsync();

        // When the admin calls a protected route.
        using var response = await client.GetAsync("/customers?page=1&pageSize=10");

        // Then authorization succeeds and the API returns the protected payload.
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
    }
}
