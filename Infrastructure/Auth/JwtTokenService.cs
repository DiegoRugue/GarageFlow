using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Infrastructure.Auth.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GarageFlow.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtTokenOptions> options) : ITokenService
{
    private const string MustChangePasswordClaimType = "must_change_password";

    private readonly JwtTokenOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public string GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        ValidateOptions(_options);

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.ExpiresMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(MustChangePasswordClaimType, user.MustChangePassword ? "true" : "false"),
            new(ClaimTypes.Name, user.FullName.Value),
            new(ClaimTypes.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.Email, user.Email.Value)
        };

        if (user.CustomerId.HasValue)
        {
            claims.Add(new Claim("customer_id", user.CustomerId.Value.Value.ToString()));
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void ValidateOptions(JwtTokenOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            throw new InvalidOperationException($"Configuration value '{JwtTokenOptions.SectionName}:Issuer' is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException($"Configuration value '{JwtTokenOptions.SectionName}:Audience' is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            throw new InvalidOperationException($"Configuration value '{JwtTokenOptions.SectionName}:Key' is required.");
        }

        if (options.ExpiresMinutes <= 0)
        {
            throw new InvalidOperationException($"Configuration value '{JwtTokenOptions.SectionName}:ExpiresMinutes' must be greater than zero.");
        }
    }
}
