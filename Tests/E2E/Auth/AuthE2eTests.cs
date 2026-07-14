using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using GarageFlow.Tests.E2E.Support.Contracts.Auth;
using GarageFlow.Tests.E2E.Support.Contracts.Common;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;

namespace GarageFlow.Tests.E2E.Auth;

[Collection(E2eApiCollection.Name)]
public sealed class AuthE2eTests(E2eApiFixture fixture)
{
    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenCredentialsAreInvalid()
    {
        // Given a known user email with an invalid password.
        using var client = _fixture.CreateClient();

        // When the login endpoint receives the invalid credentials.
        using var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(
                Email: E2eAuthSettings.BootstrapAdminEmail,
                Password: "wrong-password"));

        // Then authentication is rejected with the expected ProblemDetails contract.
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.Unauthorized);

        var problem = await HttpResponseAssertions.ReadRequiredJsonAsync<ProblemDetailsResponse>(response);
        Assert.Equal((int)HttpStatusCode.Unauthorized, problem.Status);
    }

    [Fact]
    public async Task Login_ShouldReturnTokenContract_WhenBootstrapAdminCredentialsAreValid()
    {
        // Given the bootstrap admin exists with the configured initial password.
        using var client = _fixture.CreateClient();

        // When the login endpoint receives valid credentials.
        using var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(
                Email: E2eAuthSettings.BootstrapAdminEmail,
                Password: E2eAuthSettings.BootstrapAdminInitialPassword));

        // Then the API returns a bearer token with the expected identity claims.
        HttpResponseAssertions.AssertStatus(response, HttpStatusCode.OK);

        var payload = await HttpResponseAssertions.ReadRequiredJsonAsync<LoginResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));

        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(payload.Token);
        var emailClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type is ClaimTypes.Email or JwtRegisteredClaimNames.Email);
        var roleClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type is ClaimTypes.Role or "role");

        Assert.NotNull(emailClaim);
        Assert.NotNull(roleClaim);
        Assert.Equal(E2eAuthSettings.BootstrapAdminEmail, emailClaim!.Value);
        Assert.Equal("Admin", roleClaim!.Value);
    }

    private sealed record LoginRequest(string Email, string Password);
}
