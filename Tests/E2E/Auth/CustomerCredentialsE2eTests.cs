using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GarageFlow.Tests.E2E.Support.Factories;
using GarageFlow.Tests.E2E.Support.Fixtures;
using GarageFlow.Tests.E2E.Support.Helpers;
using Microsoft.IdentityModel.Tokens;

namespace GarageFlow.Tests.E2E.Auth;

public sealed class CustomerCredentialsE2eTests(E2eApiFixture fixture) : IClassFixture<E2eApiFixture>
{
    private const string Endpoint = "/internal/auth/customer-credentials/verify";
    private const string InternalKey = "e2e-internal-auth-signing-key-abcdefghijklmnopqrstuvwxyz-0123456789";

    [Fact]
    public async Task CredentialVerification_UsesPostgresAndCurrentCustomerStatus()
    {
        await using var factory = new E2eWebApplicationFactory(
            fixture.DatabaseConnectionString, internalAuthEnabled: true, internalAuthKey: InternalKey);
        using var admin = factory.CreateClient();
        await admin.AuthenticateAsActiveBootstrapAdminAsync();
        using var create = await admin.PostAsJsonAsync("/customers", new
        {
            taxDocument = "52998224725", fullName = "Credential Customer",
            email = "credential.customer@garageflow.local", phoneNumber = "11987654321"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var customer = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var customerId = customer.RootElement.GetProperty("id").GetGuid();
        using var activate = await admin.PostAsJsonAsync(
            $"/customers/{customerId}/portal-user", new { birthDate = "1990-03-14" });
        Assert.Equal(HttpStatusCode.Created, activate.StatusCode);
        using var portal = JsonDocument.Parse(await activate.Content.ReadAsStringAsync());
        var userId = portal.RootElement.GetProperty("id").GetGuid();

        using var service = factory.CreateClient();
        SetServiceToken(service);
        using var verified = await service.PostAsJsonAsync(Endpoint, Credentials());
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        using var identity = JsonDocument.Parse(await verified.Content.ReadAsStringAsync());
        Assert.Equal(userId, identity.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(customerId, identity.RootElement.GetProperty("customerId").GetGuid());
        Assert.True(identity.RootElement.GetProperty("mustChangePassword").GetBoolean());
        Assert.False(identity.RootElement.TryGetProperty("token", out _));

        using var suspend = await admin.PatchAsJsonAsync($"/customers/{customerId}/status", new { status = "Suspended" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        SetServiceToken(service);
        using var denied = await service.PostAsJsonAsync(Endpoint, Credentials());
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var problem = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
        Assert.Equal("invalid_credentials", problem.RootElement.GetProperty("detail").GetString());

        using var reactivate = await admin.PatchAsJsonAsync($"/customers/{customerId}/status", new { status = "Active" });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        SetServiceToken(service);
        using var restored = await service.PostAsJsonAsync(Endpoint, Credentials());
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    private static object Credentials() => new { cpf = "529.982.247-25", password = "customer1990" };

    private static void SetServiceToken(HttpClient client)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: "GarageFlow.Serverless",
            audience: "GarageFlow.InternalAuth",
            claims:
            [
                new Claim("sub", "customer-auth-function"),
                new Claim("scope", "customer-credentials:verify"),
                new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
            ],
            notBefore: now,
            expires: now.AddSeconds(60),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(InternalKey)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }
}
