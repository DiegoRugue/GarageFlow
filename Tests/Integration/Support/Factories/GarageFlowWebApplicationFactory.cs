using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GarageFlow.Tests.Integration.Support.Helpers;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Globalization;

namespace GarageFlow.Tests.Integration.Support.Factories;

public sealed class GarageFlowWebApplicationFactory(string databaseName, bool disableAutoMigrate = false)
    : WebApplicationFactory<Program>
{
    private readonly string _databaseName = databaseName;
    private readonly bool _disableAutoMigrate = disableAutoMigrate;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
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
                ["Auth:BootstrapAdmin:Password"] = IntegrationTestAuthSettings.BootstrapAdminInitialPassword
            };

            if (_disableAutoMigrate)
            {
                settings["Database:AutoMigrate"] = "false";
            }

            configurationBuilder.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            services.PostConfigureAll<JwtBearerOptions>(options =>
            {
                options.TokenValidationParameters.ValidIssuer = IntegrationTestAuthSettings.JwtIssuer;
                options.TokenValidationParameters.ValidAudience = IntegrationTestAuthSettings.JwtAudience;
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IntegrationTestAuthSettings.JwtKey));
            });
        });
    }
}
