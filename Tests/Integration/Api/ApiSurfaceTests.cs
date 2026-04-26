using System.Net;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;

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

    private sealed record ProblemDetailsPayload(string? Title, int? Status);
}
