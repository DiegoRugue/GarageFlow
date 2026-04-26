using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
                ["Database:DatabaseName"] = _databaseName
            };

            if (_disableAutoMigrate)
            {
                settings["Database:AutoMigrate"] = "false";
            }

            configurationBuilder.AddInMemoryCollection(settings);
        });
    }
}
