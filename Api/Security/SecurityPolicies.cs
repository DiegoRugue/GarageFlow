namespace GarageFlow.Api.Security;

public static class SecurityPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string ActiveUser = nameof(ActiveUser);
    public const string ActiveAdmin = nameof(ActiveAdmin);
    public const string ActiveAttendant = nameof(ActiveAttendant);
}
