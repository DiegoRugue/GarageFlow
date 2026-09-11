namespace GarageFlow.Adapters.Api.Security;

public static class InternalAuth
{
    public const string SectionName = "Auth:Internal";
    public const string Scheme = "InternalService";
    public const string Issuer = "GarageFlow.Serverless";
    public const string Audience = "GarageFlow.InternalAuth";
    public const string Subject = "customer-auth-function";
    public const string Scope = "customer-credentials:verify";
    public const int MaximumLifetimeSeconds = 60;
}
