namespace GarageFlow.Adapters.Infrastructure.Auth;

public sealed class BootstrapAdminOptions
{
    public const string SectionName = "Auth:BootstrapAdmin";

    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string BirthDate { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
