using GarageFlow.Adapters.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GarageFlow.Tests.Integration.Api.Security;

public class AuthenticationExtensionsTests
{
    [Fact]
    public void AddGarageFlowAuthentication_ShouldConfigureJwtValidationAndAuthorizationPolicies()
    {
        var builder = CreateBuilder(environmentName: "Production");

        builder.AddGarageFlowAuthentication();

        using var app = builder.Build();
        var jwtOptions = app.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var authorizationOptions = app.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        Assert.True(jwtOptions.TokenValidationParameters.ValidateIssuer);
        Assert.Equal("garageflow-tests", jwtOptions.TokenValidationParameters.ValidIssuer);
        Assert.True(jwtOptions.TokenValidationParameters.ValidateAudience);
        Assert.Equal("garageflow-api-tests", jwtOptions.TokenValidationParameters.ValidAudience);
        Assert.True(jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.IsType<SymmetricSecurityKey>(jwtOptions.TokenValidationParameters.IssuerSigningKey);
        Assert.True(jwtOptions.TokenValidationParameters.ValidateLifetime);
        Assert.Equal(TimeSpan.Zero, jwtOptions.TokenValidationParameters.ClockSkew);

        var activeCustomerPolicy = authorizationOptions.GetPolicy(SecurityPolicies.ActiveCustomer);

        Assert.NotNull(activeCustomerPolicy);
        Assert.Contains(activeCustomerPolicy.Requirements, requirement =>
            requirement is ClaimsAuthorizationRequirement claimsRequirement &&
            claimsRequirement.ClaimType == SecurityClaimTypes.MustChangePassword &&
            claimsRequirement.AllowedValues?.Contains("false") == true);
    }

    [Fact]
    public void AddGarageFlowAuthentication_ShouldThrowInvalidOperationException_WhenJwtExpiresMinutesIsInvalid()
    {
        var builder = CreateBuilder(environmentName: "Production", expiresMinutes: "0");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddGarageFlowAuthentication());

        Assert.Equal("Configuration value 'Auth:Jwt:ExpiresMinutes' must be greater than zero.", exception.Message);
    }

    [Fact]
    public void AddGarageFlowAuthentication_ShouldThrowInvalidOperationException_WhenProductionJwtKeyIsPlaceholder()
    {
        var builder = CreateBuilder(environmentName: "Production", jwtKey: "__set_me_jwt_key__");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddGarageFlowAuthentication());

        Assert.Contains("Auth:Jwt:Key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddGarageFlowAuthentication_ShouldThrowInvalidOperationException_WhenProductionBootstrapPasswordIsPlaceholder()
    {
        var builder = CreateBuilder(environmentName: "Production", bootstrapAdminPassword: "changeme");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddGarageFlowAuthentication());

        Assert.Contains("Auth:BootstrapAdmin:Password", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddGarageFlowAuthentication_ShouldAllowPlaceholderSecrets_WhenEnvironmentIsIntegrationTests()
    {
        var builder = CreateBuilder(
            environmentName: "IntegrationTests",
            jwtKey: "__set_me_jwt_key__",
            bootstrapAdminPassword: "changeme");

        var result = builder.AddGarageFlowAuthentication();

        Assert.Same(builder, result);
    }

    private static WebApplicationBuilder CreateBuilder(
        string environmentName,
        string expiresMinutes = "60",
        string jwtKey = "garageflow-tests-secure-jwt-signing-key",
        string bootstrapAdminPassword = "garageflow-tests-secure-bootstrap-password")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:Jwt:Issuer"] = "garageflow-tests",
            ["Auth:Jwt:Audience"] = "garageflow-api-tests",
            ["Auth:Jwt:Key"] = jwtKey,
            ["Auth:Jwt:ExpiresMinutes"] = expiresMinutes,
            ["Auth:BootstrapAdmin:Password"] = bootstrapAdminPassword
        });

        return builder;
    }
}
