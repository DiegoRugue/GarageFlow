namespace GarageFlow.Tests.E2E.Support.Helpers;

public static class E2eAuthSettings
{
    public const string EstimateDecisionWebhookSecret = "e2e-webhook-secret-value-32-characters-minimum";
    public const string JwtIssuer = "GarageFlow.E2ETests";
    public const string JwtAudience = "GarageFlow.E2ETests.Api";
    public const string JwtKey = "garageflow.e2etests.jwt.key.2026";
    public const int JwtExpiresMinutes = 120;

    public const string BootstrapAdminFullName = "E2E Admin";
    public const string BootstrapAdminEmail = "e2e-admin@garageflow.local";
    public const string BootstrapAdminBirthDate = "1990-01-01";
    public const string BootstrapAdminInitialPassword = "E2E.Admin#123";
    public const string BootstrapAdminActivePassword = "E2E.Admin#456";
}
