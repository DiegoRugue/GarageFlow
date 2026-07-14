using System.Net;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Integration.Support.Factories;

namespace GarageFlow.Tests.Integration.Api;

public class ApiSurfaceTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task OpenApiEndpoint_ShouldReturnSuccess()
    {
        using var client = _fixture.CreateClient(disableAutoMigrate: true);

        var response = await client.GetAsync("/openapi/v1.json");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task ScalarRoute_ShouldReturnSuccess()
    {
        using var client = _fixture.CreateClient(disableAutoMigrate: true);

        var response = await client.GetAsync("/scalar");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExceptionMiddleware_ShouldReturnProblemDetails_WhenUnhandledExceptionIsThrown()
    {
        using var client = _fixture.CreateClient(disableAutoMigrate: true);

        var response = await client.GetAsync("/integration-tests/throw/unhandled");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.InternalServerError);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsPayload>(response);
        Assert.Equal("Unexpected error", payload.Title);
        Assert.Equal((int)HttpStatusCode.InternalServerError, payload.Status);
    }

    [Fact]
    public async Task HealthEndpoint_ShouldPreserveBackwardCompatiblePayload()
    {
        using var client = _fixture.CreateClient(disableAutoMigrate: true);

        using var response = await client.GetAsync("/health");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        Assert.Equal("{\"status\":\"ok\"}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LivenessEndpoint_ShouldBeHealthyWithoutRunningDatabaseChecks()
    {
        await using var factory = new GarageFlowWebApplicationFactory(
            $"broken-live-{Guid.NewGuid():N}",
            disableAutoMigrate: true,
            environmentName: "Integration",
            databaseProvider: "Postgres",
            connectionString: "Host=127.0.0.1;Port=1;Database=unavailable;Username=x;Password=x;Timeout=1");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessEndpoint_ShouldBeHealthy_WhenDatabaseCanConnect()
    {
        using var client = _fixture.CreateClient(disableAutoMigrate: false);

        using var response = await client.GetAsync("/health/ready");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessEndpoint_ShouldBeUnavailableWithoutLeakingDetails_WhenDatabaseCannotConnect()
    {
        const string connectionString = "Host=127.0.0.1;Port=1;Database=unavailable;Username=secret-user;Password=secret-password;Timeout=1";
        await using var factory = new GarageFlowWebApplicationFactory(
            $"broken-ready-{Guid.NewGuid():N}",
            disableAutoMigrate: true,
            environmentName: "Integration",
            databaseProvider: "Postgres",
            connectionString: connectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-user", body, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password", body, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", body, StringComparison.Ordinal);
    }

    private sealed record ProblemDetailsPayload(string? Title, int? Status);
}
