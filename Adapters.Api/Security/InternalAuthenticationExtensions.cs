using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GarageFlow.Adapters.Api.Security;

public static class InternalAuthenticationExtensions
{
    private const int MinimumKeyBytes = 32;

    public static WebApplicationBuilder AddGarageFlowInternalAuthentication(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var section = builder.Configuration.GetSection(InternalAuth.SectionName);
        if (!section.GetValue<bool>("Enabled"))
        {
            return builder;
        }

        var key = section["Key"];
        if (!IsValidKey(key, builder.Configuration["Auth:Jwt:Key"]))
        {
            throw new InvalidOperationException(
                "Configuration 'Auth:Internal:Key' requires a non-placeholder key of at least 32 UTF-8 bytes, different from the user JWT key.");
        }

        builder.Services.AddAuthentication()
            .AddJwtBearer(InternalAuth.Scheme, options =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = InternalAuth.Issuer,
                    ValidateAudience = true,
                    ValidAudience = InternalAuth.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var clock = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
                        if (!InternalTokenLifetime.IsValid(context.Principal, clock.GetUtcNow()))
                        {
                            context.Fail("Invalid service token lifetime.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorizationBuilder().AddPolicy(
            SecurityPolicies.VerifyCustomerCredentials,
            policy => policy
                .AddAuthenticationSchemes(InternalAuth.Scheme)
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    HasSingleClaim(context.User, "sub", InternalAuth.Subject)
                    && HasSingleClaim(context.User, "scope", InternalAuth.Scope)));

        return builder;
    }

    private static bool HasSingleClaim(ClaimsPrincipal principal, string type, string value)
    {
        var claims = principal.FindAll(type).ToArray();
        return claims.Length == 1 && string.Equals(claims[0].Value, value, StringComparison.Ordinal);
    }

    private static bool IsValidKey(string? key, string? userKey)
    {
        if (string.IsNullOrWhiteSpace(key)
            || Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes
            || string.Equals(key, userKey, StringComparison.Ordinal))
        {
            return false;
        }

        string[] placeholders = ["set_me", "changeme", "replace", "placeholder", "<set-me>", "example"];
        return !placeholders.Any(value => key.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
