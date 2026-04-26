using System.Net;
using System.Net.Http.Json;
using GarageFlow.Tests.Integration.Api.Auth.Contracts;
using GarageFlow.Tests.Integration.Support.Fixtures;
using GarageFlow.Tests.Integration.Support.Helpers;

namespace GarageFlow.Tests.Integration.Api.Auth;

public class AuthApiTests(GarageFlowApiFixture fixture) : IClassFixture<GarageFlowApiFixture>
{
    private readonly GarageFlowApiFixture _fixture = fixture;

    [Fact]
    public async Task Login_ShouldReturn200AndToken_WhenCredentialsAreValid()
    {
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(
                Email: IntegrationTestAuthSettings.BootstrapAdminEmail,
                Password: IntegrationTestAuthSettings.BootstrapAdminInitialPassword));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);
        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<LoginResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
        Assert.True(payload.MustChangePassword);
    }

    [Fact]
    public async Task Login_ShouldReturn401_WhenCredentialsAreInvalid()
    {
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(
                Email: IntegrationTestAuthSettings.BootstrapAdminEmail,
                Password: "invalid-password"));

        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);
    }

    private sealed record LoginRequest(string Email, string Password);
}
