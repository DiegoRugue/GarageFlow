using System.Globalization;
using System.Text;
using Amazon.SimpleNotificationService;
using GarageFlow.Tests.Integration.Support.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace GarageFlow.Tests.Integration.Support.Factories;

public sealed class GarageFlowWebApplicationFactory(
    string databaseName,
    bool disableAutoMigrate = false,
    string estimateDecisionWebhookSecret = IntegrationTestAuthSettings.EstimateDecisionWebhookSecret,
    string environmentName = "IntegrationTests",
    bool outboxEnabled = false,
    string? snsRegion = null,
    string? snsTopicArn = null,
    IAmazonSimpleNotificationService? snsClient = null)
    : WebApplicationFactory<Program>
{
    private readonly string _databaseName = databaseName;
    private readonly bool _disableAutoMigrate = disableAutoMigrate;
    private readonly string _estimateDecisionWebhookSecret = estimateDecisionWebhookSecret;
    private readonly string _environmentName = environmentName;
    private readonly bool _outboxEnabled = outboxEnabled;
    private readonly string? _snsRegion = snsRegion;
    private readonly string? _snsTopicArn = snsTopicArn;
    private readonly IAmazonSimpleNotificationService? _snsClient = snsClient;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configurationBuilder =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Integrations:Outbox:Enabled"] = _outboxEnabled.ToString(CultureInfo.InvariantCulture),
                ["Integrations:Sns:Region"] = _snsRegion,
                ["Integrations:Sns:TopicArn"] = _snsTopicArn
            });
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(BuildSettings());
        });

        builder.ConfigureServices(services =>
        {
            if (_snsClient is not null)
            {
                services.AddSingleton(_snsClient);
            }

            services.PostConfigureAll<JwtBearerOptions>(options =>
            {
                options.TokenValidationParameters.ValidIssuer = IntegrationTestAuthSettings.JwtIssuer;
                options.TokenValidationParameters.ValidAudience = IntegrationTestAuthSettings.JwtAudience;
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IntegrationTestAuthSettings.JwtKey));
            });
        });
    }

    private Dictionary<string, string?> BuildSettings()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "InMemory",
            ["Database:DatabaseName"] = _databaseName,
            ["Auth:Jwt:Issuer"] = IntegrationTestAuthSettings.JwtIssuer,
            ["Auth:Jwt:Audience"] = IntegrationTestAuthSettings.JwtAudience,
            ["Auth:Jwt:Key"] = IntegrationTestAuthSettings.JwtKey,
            ["Auth:Jwt:ExpiresMinutes"] = IntegrationTestAuthSettings.JwtExpiresMinutes.ToString(CultureInfo.InvariantCulture),
            ["Auth:BootstrapAdmin:FullName"] = IntegrationTestAuthSettings.BootstrapAdminFullName,
            ["Auth:BootstrapAdmin:Email"] = IntegrationTestAuthSettings.BootstrapAdminEmail,
            ["Auth:BootstrapAdmin:BirthDate"] = IntegrationTestAuthSettings.BootstrapAdminBirthDate,
            ["Auth:BootstrapAdmin:Password"] = IntegrationTestAuthSettings.BootstrapAdminInitialPassword,
            ["Webhooks:EstimateDecisions:HmacSecret"] = _estimateDecisionWebhookSecret,
            ["Integrations:Outbox:Enabled"] = _outboxEnabled.ToString(CultureInfo.InvariantCulture),
            ["Integrations:Sns:Region"] = _snsRegion,
            ["Integrations:Sns:TopicArn"] = _snsTopicArn
        };

        if (_disableAutoMigrate)
        {
            settings["Database:AutoMigrate"] = "false";
        }

        return settings;
    }
}
