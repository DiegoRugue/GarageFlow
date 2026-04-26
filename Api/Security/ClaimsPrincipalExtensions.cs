using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GarageFlow.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(subject, out var userId))
        {
            throw new UnauthorizedAccessException("Authenticated user identifier claim is missing or invalid.");
        }

        return userId;
    }
}
