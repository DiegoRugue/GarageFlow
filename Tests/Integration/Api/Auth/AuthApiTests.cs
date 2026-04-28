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

        var payload = await TryLoginWithBootstrapAdminPasswordsAsync(client);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
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

    private static async Task<LoginResponse> TryLoginWithBootstrapAdminPasswordsAsync(HttpClient client)
    {
        var candidatePasswords = new[]
        {
            IntegrationTestAuthSettings.BootstrapAdminInitialPassword,
            IntegrationTestAuthSettings.BootstrapAdminActivePassword
        };

        foreach (var password in candidatePasswords)
        {
            var response = await client.PostAsJsonAsync(
                "/auth/login",
                new LoginRequest(
                    Email: IntegrationTestAuthSettings.BootstrapAdminEmail,
                    Password: password));

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return await HttpResponseAssertions.ReadRequiredJsonAsync<LoginResponse>(response);
            }

            response.Dispose();
        }

        throw new Xunit.Sdk.XunitException(
            "Expected bootstrap admin login to succeed with an accepted password, but all attempts returned Unauthorized.");
    }
}
