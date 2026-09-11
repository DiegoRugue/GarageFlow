using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Tests.Integration.Support.Factories;
using GarageFlow.Tests.Integration.Support.Helpers;
using GarageFlow.Tests.Shared.Customers;
using GarageFlow.Tests.Shared.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace GarageFlow.Tests.Integration.Api.Auth;

public sealed class CustomerCredentialsApiTests
{
    private const string Endpoint = "/internal/auth/customer-credentials/verify";
    private const string InternalKey = "internal-auth-tests-key-abcdefghijklmnopqrstuvwxyz-0123456789-ABCDEF";
    private const string Password = "Portal.Test#123";
    private const string Cpf = "52998224725";

    [Fact]
    public async Task PrivateRoute_IsAbsentByDefault()
    {
        await using var factory = new GarageFlowWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(Endpoint, new { cpf = Cpf, password = Password });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(true, "529.982.247-25")]
    [InlineData(false, Cpf)]
    public async Task ValidServiceTokenAndCredentials_ReturnOnlyPortalIdentity(bool mustChangePassword, string cpf)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, customerId) = await SeedPortalUserAsync(factory, mustChangePassword);
        SetToken(client, CreateToken());

        using var response = await client.PostAsJsonAsync(Endpoint, new { cpf, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(userId, payload.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(customerId, payload.RootElement.GetProperty("customerId").GetGuid());
        Assert.Equal("Customer", payload.RootElement.GetProperty("role").GetString());
        Assert.Equal(mustChangePassword, payload.RootElement.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(
            ["customerId", "mustChangePassword", "role", "userId"],
            payload.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingCustomerAndWrongPassword_ReturnSameGenericFailure(bool existingCustomer)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        if (existingCustomer)
        {
            await SeedPortalUserAsync(factory, false);
        }
        SetToken(client, CreateToken());

        using var response = await client.PostAsJsonAsync(Endpoint, new { cpf = Cpf, password = "Incorrect#123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_credentials", payload.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain(Cpf, payload.RootElement.GetRawText());
        Assert.DoesNotContain("Incorrect#123", payload.RootElement.GetRawText());
    }

    [Theory]
    [InlineData("12345678900", Password)]
    [InlineData("11222333000181", Password)]
    [InlineData("", Password)]
    [InlineData(Cpf, "")]
    [InlineData(Cpf, "   ")]
    public async Task InvalidInput_Returns400(string cpf, string password)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        SetToken(client, CreateToken());

        using var response = await client.PostAsJsonAsync(Endpoint, new { cpf, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"cpf\":\"52998224725\"}")]
    [InlineData("{")]
    public async Task MissingFieldsOrMalformedJson_Return400(string body)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        SetToken(client, CreateToken());
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(Endpoint, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PrivateRoute_IsExcludedFromPublicOpenApi()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(document.RootElement.GetProperty("paths").EnumerateObject(),
            path => path.Name.StartsWith("/internal/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingToken_AndOrdinaryUserToken_CannotVerifyCredentials()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var anonymous = await client.PostAsJsonAsync(Endpoint, new { cpf = Cpf, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        await client.AuthenticateAsActiveBootstrapAdminAsync();
        using var userResponse = await client.PostAsJsonAsync(Endpoint, new { cpf = Cpf, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, userResponse.StatusCode);
    }

    [Theory]
    [InlineData("issuer", HttpStatusCode.Unauthorized)]
    [InlineData("audience", HttpStatusCode.Unauthorized)]
    [InlineData("signature", HttpStatusCode.Unauthorized)]
    [InlineData("algorithm", HttpStatusCode.Unauthorized)]
    [InlineData("unsigned", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("future", HttpStatusCode.Unauthorized)]
    [InlineData("long-lived", HttpStatusCode.Unauthorized)]
    [InlineData("missing-exp", HttpStatusCode.Unauthorized)]
    [InlineData("missing-iat", HttpStatusCode.Unauthorized)]
    [InlineData("missing-nbf", HttpStatusCode.Unauthorized)]
    [InlineData("future-iat", HttpStatusCode.Unauthorized)]
    [InlineData("invalid-iat", HttpStatusCode.Unauthorized)]
    [InlineData("subject", HttpStatusCode.Forbidden)]
    [InlineData("scope", HttpStatusCode.Forbidden)]
    [InlineData("duplicate-subject", HttpStatusCode.Unauthorized)]
    [InlineData("missing-subject", HttpStatusCode.Forbidden)]
    [InlineData("missing-scope", HttpStatusCode.Forbidden)]
    [InlineData("duplicate-scope", HttpStatusCode.Forbidden)]
    public async Task InvalidServiceIdentity_IsRejected(string mutation, HttpStatusCode expectedStatus)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        SetToken(client, CreateToken(mutation));

        using var response = await client.PostAsJsonAsync(Endpoint, new { cpf = Cpf, password = Password });

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task ServiceToken_CannotAuthenticateBusinessEndpoints()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        SetToken(client, CreateToken());

        using var response = await client.GetAsync("/customers?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("__SET_ME_INTERNAL_AUTH_KEY_WITH_32_CHARACTERS__")]
    [InlineData(IntegrationTestAuthSettings.JwtKey)]
    public void EnablingWithMissingWeakOrSharedKey_FailsStartup(string? key)
    {
        using var factory = new GarageFlowWebApplicationFactory(
            Guid.NewGuid().ToString(), internalAuthEnabled: true, internalAuthKey: key);

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    private static GarageFlowWebApplicationFactory CreateFactory() =>
        new(Guid.NewGuid().ToString(), internalAuthEnabled: true, internalAuthKey: InternalKey);

    private static async Task<(Guid UserId, Guid CustomerId)> SeedPortalUserAsync(
        GarageFlowWebApplicationFactory factory, bool mustChangePassword)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var hashService = scope.ServiceProvider.GetRequiredService<IPasswordHashService>();
        var customer = new CustomerBuilder().Build();
        var user = new UserBuilder().WithRole(UserRole.Customer).WithCustomerId(customer.Id)
            .WithPasswordHash(hashService.Hash(Password)).WithMustChangePassword(mustChangePassword).Build();
        db.Customers.Add(customer);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id.Value, customer.Id.Value);
    }

    private static void SetToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static string CreateToken(string? mutation = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new JwtPayload
        {
            ["iss"] = "GarageFlow.Serverless", ["aud"] = "GarageFlow.InternalAuth",
            ["sub"] = "customer-auth-function", ["scope"] = "customer-credentials:verify",
            ["iat"] = now - 5, ["nbf"] = now - 5, ["exp"] = now + 30
        };
        switch (mutation)
        {
            case "issuer": payload["iss"] = "untrusted"; break;
            case "audience": payload["aud"] = "untrusted"; break;
            case "subject": payload["sub"] = "different-function"; break;
            case "scope": payload["scope"] = "different:permission"; break;
            case "duplicate-subject": payload["sub"] = new[] { "customer-auth-function", "different-function" }; break;
            case "missing-subject": payload.Remove("sub"); break;
            case "missing-scope": payload.Remove("scope"); break;
            case "duplicate-scope": payload["scope"] = new[] { "customer-credentials:verify", "different:permission" }; break;
            case "expired": payload["iat"] = now - 90; payload["nbf"] = now - 90; payload["exp"] = now - 30; break;
            case "future": payload["iat"] = now + 30; payload["nbf"] = now + 30; payload["exp"] = now + 60; break;
            case "future-iat": payload["iat"] = now + 10; break;
            case "long-lived": payload["exp"] = now + 120; break;
            case "missing-exp": payload.Remove("exp"); break;
            case "missing-iat": payload.Remove("iat"); break;
            case "missing-nbf": payload.Remove("nbf"); break;
            case "invalid-iat": payload["iat"] = "invalid"; break;
        }
        var key = mutation == "signature" ? new string('x', 64) : InternalKey;
        var algorithm = mutation == "algorithm" ? SecurityAlgorithms.HmacSha512 : SecurityAlgorithms.HmacSha256;
        var credentials = mutation == "unsigned" ? null :
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), algorithm);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
    }
}
