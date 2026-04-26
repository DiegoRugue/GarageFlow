namespace GarageFlow.Infrastructure.Auth.Jwt;

public sealed class JwtTokenOptions
{
    public const string SectionName = "Auth:Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public int ExpiresMinutes { get; init; } = 60;
}
