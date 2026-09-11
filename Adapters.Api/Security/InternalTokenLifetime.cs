using System.Globalization;
using System.Security.Claims;

namespace GarageFlow.Adapters.Api.Security;

internal static class InternalTokenLifetime
{
    public static bool IsValid(ClaimsPrincipal? principal, DateTimeOffset now)
    {
        if (principal is null
            || !TryGetTimestamp(principal, "iat", out var issuedAt)
            || !TryGetTimestamp(principal, "nbf", out var notBefore)
            || !TryGetTimestamp(principal, "exp", out var expiresAt))
        {
            return false;
        }

        var currentTime = now.ToUnixTimeSeconds();
        return issuedAt <= currentTime
            && notBefore <= currentTime
            && expiresAt > currentTime
            && expiresAt > issuedAt
            && expiresAt > notBefore
            && expiresAt - Math.Min(issuedAt, notBefore) <= InternalAuth.MaximumLifetimeSeconds;
    }

    private static bool TryGetTimestamp(ClaimsPrincipal principal, string type, out long timestamp)
    {
        timestamp = 0;
        var claims = principal.FindAll(type).ToArray();
        return claims.Length == 1
            && long.TryParse(claims[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out timestamp);
    }
}
