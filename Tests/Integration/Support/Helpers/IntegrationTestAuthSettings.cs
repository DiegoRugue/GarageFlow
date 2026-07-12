namespace GarageFlow.Tests.Integration.Support.Helpers;

public static class IntegrationTestAuthSettings
{
    public const string EstimateDecisionWebhookSecret = "integration-webhook-secret-32-characters-minimum";
    public const string JwtIssuer = "GarageFlow.IntegrationTests";
    public const string JwtAudience = "GarageFlow.IntegrationTests.Api";
    public const string JwtKey = "garageflow.integrationtests.jwt.key.2026";
    public const int JwtExpiresMinutes = 120;

    public const string BootstrapAdminFullName = "Integration Admin";
    public const string BootstrapAdminEmail = "integration-admin@garageflow.local";
    public const string BootstrapAdminBirthDate = "1990-01-01";
    public const string BootstrapAdminInitialPassword = "Integration.Admin#123";
    public const string BootstrapAdminActivePassword = "Integration.Admin#456";
}
