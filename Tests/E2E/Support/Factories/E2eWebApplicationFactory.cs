using System.Globalization;
using System.Text;
using GarageFlow.Tests.E2E.Support.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace GarageFlow.Tests.E2E.Support.Factories;

public sealed class E2eWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = connectionString;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configurationBuilder =>
        {
            configurationBuilder.AddInMemoryCollection(BuildSettings());
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("E2ETests");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(BuildSettings());
        });

        builder.ConfigureServices(services =>
        {
            services.PostConfigureAll<JwtBearerOptions>(options =>
            {
                options.TokenValidationParameters.ValidIssuer = E2eAuthSettings.JwtIssuer;
                options.TokenValidationParameters.ValidAudience = E2eAuthSettings.JwtAudience;
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(E2eAuthSettings.JwtKey));
            });
        });
    }

    private Dictionary<string, string?> BuildSettings()
    {
        return new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Postgres",
            ["Database:AutoMigrate"] = "true",
            ["ConnectionStrings:GarageFlow"] = _connectionString,
            ["Auth:Jwt:Issuer"] = E2eAuthSettings.JwtIssuer,
            ["Auth:Jwt:Audience"] = E2eAuthSettings.JwtAudience,
            ["Auth:Jwt:Key"] = E2eAuthSettings.JwtKey,
            ["Auth:Jwt:ExpiresMinutes"] = E2eAuthSettings.JwtExpiresMinutes.ToString(CultureInfo.InvariantCulture),
            ["Auth:BootstrapAdmin:FullName"] = E2eAuthSettings.BootstrapAdminFullName,
            ["Auth:BootstrapAdmin:Email"] = E2eAuthSettings.BootstrapAdminEmail,
            ["Auth:BootstrapAdmin:BirthDate"] = E2eAuthSettings.BootstrapAdminBirthDate,
            ["Auth:BootstrapAdmin:Password"] = E2eAuthSettings.BootstrapAdminInitialPassword
        };
    }
}
