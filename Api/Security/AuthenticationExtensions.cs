using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace GarageFlow.Api.Security;

public static class AuthenticationExtensions
{
    private const string MustChangePasswordFalseValue = "false";

    public static WebApplicationBuilder AddGarageFlowAuthentication(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var jwtSection = builder.Configuration.GetSection("Auth:Jwt");
        var jwtIssuer = GetRequiredConfigurationValue(jwtSection["Issuer"], "Auth:Jwt:Issuer");
        var jwtAudience = GetRequiredConfigurationValue(jwtSection["Audience"], "Auth:Jwt:Audience");
        var jwtKey = GetRequiredConfigurationValue(jwtSection["Key"], "Auth:Jwt:Key");
        var bootstrapAdminPassword = builder.Configuration["Auth:BootstrapAdmin:Password"];

        if (!int.TryParse(jwtSection["ExpiresMinutes"], out var jwtExpiresMinutes) || jwtExpiresMinutes <= 0)
        {
            throw new InvalidOperationException("Configuration value 'Auth:Jwt:ExpiresMinutes' must be greater than zero.");
        }

        var shouldFailFastForSecrets = !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("IntegrationTests");
        if (shouldFailFastForSecrets && IsMissingOrPlaceholderSecret(jwtKey))
        {
            throw new InvalidOperationException(
                "Configuration value 'Auth:Jwt:Key' is missing or appears to be a placeholder. " +
                "Set a secure value before starting in this environment.");
        }

        if (shouldFailFastForSecrets && IsMissingOrPlaceholderSecret(bootstrapAdminPassword))
        {
            throw new InvalidOperationException(
                "Configuration value 'Auth:BootstrapAdmin:Password' is missing or appears to be a placeholder. " +
                "Set a secure value before starting in this environment.");
        }

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
            });

        var authorizationBuilder = builder.Services.AddAuthorizationBuilder();
        authorizationBuilder.AddPolicy(
            SecurityPolicies.AdminOnly,
            policy => policy.RequireRole(SecurityRoles.Admin));

        authorizationBuilder.AddPolicy(
            SecurityPolicies.ActiveUser,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(SecurityClaimTypes.MustChangePassword, MustChangePasswordFalseValue));

        authorizationBuilder.AddPolicy(
            SecurityPolicies.ActiveAdmin,
            policy => policy
                .RequireRole(SecurityRoles.Admin)
                .RequireClaim(SecurityClaimTypes.MustChangePassword, MustChangePasswordFalseValue));

        authorizationBuilder.AddPolicy(
            SecurityPolicies.ActiveAttendant,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(SecurityRoles.Attendant)
                .RequireClaim(SecurityClaimTypes.MustChangePassword, MustChangePasswordFalseValue));

        authorizationBuilder.AddPolicy(
            SecurityPolicies.ActiveCustomer,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(SecurityRoles.Customer)
                .RequireClaim(SecurityClaimTypes.MustChangePassword, MustChangePasswordFalseValue));

        authorizationBuilder.AddPolicy(
            SecurityPolicies.ActiveStaff,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(SecurityRoles.Admin, SecurityRoles.Attendant)
                .RequireClaim(SecurityClaimTypes.MustChangePassword, MustChangePasswordFalseValue));

        return builder;
    }

    private static string GetRequiredConfigurationValue(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{key}' is required.");
        }

        return value;
    }

    private static bool IsMissingOrPlaceholderSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("__set_me_", StringComparison.Ordinal) && normalized.EndsWith("__", StringComparison.Ordinal))
        {
            return true;
        }

        var placeholderTokens = new[]
        {
            "set_me",
            "changeme",
            "replace",
            "placeholder",
            "__set_me__",
            "<set-me>",
            "example"
        };

        return placeholderTokens.Any(token => normalized.Contains(token));
    }
}
