using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GarageFlow.Adapters.Infrastructure.DataAccess;

public sealed class GarageFlowDbContextFactory : IDesignTimeDbContextFactory<GarageFlowDbContext>
{
    public GarageFlowDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var configurationBasePath = ResolveConfigurationBasePath();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(configurationBasePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<GarageFlowDbContext>();
        var databaseProvider = ResolveDatabaseProvider(configuration["Database:Provider"]);
        var useInMemoryProvider =
            databaseProvider == "InMemory" ||
            string.Equals(environment, "IntegrationTests", StringComparison.OrdinalIgnoreCase);

        if (useInMemoryProvider)
        {
            var databaseName = configuration["Database:DatabaseName"];
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = "garageflow";
            }

            optionsBuilder.UseInMemoryDatabase(databaseName);
        }
        else
        {
            var connectionString = configuration.GetConnectionString("GarageFlow");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Missing required connection string 'ConnectionStrings:GarageFlow' for Postgres provider. " +
                    "Configure it via appsettings or the environment variable 'ConnectionStrings__GarageFlow'.");
            }

            optionsBuilder.UseNpgsql(connectionString);
        }

        return new GarageFlowDbContext(optionsBuilder.Options);
    }

    private static string ResolveDatabaseProvider(string? configuredProvider)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            return "Postgres";
        }

        if (string.Equals(configuredProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return "InMemory";
        }

        if (string.Equals(configuredProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            return "Postgres";
        }

        throw new InvalidOperationException(
            $"Invalid value for configuration key 'Database:Provider': '{configuredProvider}'. " +
            "Allowed values are 'InMemory', 'Postgres', or empty/null (defaults to 'Postgres').");
    }

    private static string ResolveConfigurationBasePath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var currentDirectoryAppsettings = Path.Combine(currentDirectory, "appsettings.json");
        if (File.Exists(currentDirectoryAppsettings))
        {
            return currentDirectory;
        }

        var adaptersApiDirectory = Path.Combine(currentDirectory, "Adapters.Api");
        var adaptersApiDirectoryAppsettings = Path.Combine(adaptersApiDirectory, "appsettings.json");
        if (File.Exists(adaptersApiDirectoryAppsettings))
        {
            return adaptersApiDirectory;
        }

        return currentDirectory;
    }
}
