namespace GarageFlow.Adapters.Api.Security;

public static class SecurityPolicies
{
    public const string VerifyCustomerCredentials = nameof(VerifyCustomerCredentials);
    public const string AdminOnly = nameof(AdminOnly);
    public const string ActiveUser = nameof(ActiveUser);
    public const string ActiveAdmin = nameof(ActiveAdmin);
    public const string ActiveAttendant = nameof(ActiveAttendant);
    public const string ActiveCustomer = nameof(ActiveCustomer);
    public const string ActiveStaff = nameof(ActiveStaff);
}
