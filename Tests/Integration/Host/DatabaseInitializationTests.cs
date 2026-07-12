using GarageFlow.Adapters.Infrastructure.Auth;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace GarageFlow.Tests.Integration.Host;

public sealed class DatabaseInitializationTests
{
    [Fact]
    public async Task AutoMigrateGarageFlowAsync_ShouldNotInitializeDatabase_WhenAutoMigrateIsFalse()
    {
        var root = new InMemoryDatabaseRoot();
        await using var services = CreateServiceProvider(root);
        var configuration = CreateConfiguration(autoMigrate: false);

        await services.AutoMigrateGarageFlowAsync(configuration, CreateEnvironment(), Directory.GetCurrentDirectory());

        await using var scope = services.CreateAsyncScope();
        var creator = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>()
            .GetService<IDatabaseCreator>();
        Assert.True(await creator.EnsureCreatedAsync());
    }

    [Fact]
    public async Task MigrateGarageFlowAsync_ShouldInitializeDatabaseAndBootstrapAdmin_WhenAutoMigrateIsFalse()
    {
        var root = new InMemoryDatabaseRoot();
        await using var services = CreateServiceProvider(root);
        var configuration = CreateConfiguration(autoMigrate: false);

        var returnedServices = await services.MigrateGarageFlowAsync(
            configuration,
            CreateEnvironment(),
            Directory.GetCurrentDirectory());

        Assert.Same(services, returnedServices);
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        Assert.False(await dbContext.GetService<IDatabaseCreator>().EnsureCreatedAsync());
        Assert.Single(await dbContext.Users.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task MigrateGarageFlowAsync_ShouldBeIdempotent()
    {
        var root = new InMemoryDatabaseRoot();
        await using var services = CreateServiceProvider(root);
        var configuration = CreateConfiguration(autoMigrate: false);

        await services.MigrateGarageFlowAsync(configuration, CreateEnvironment(), Directory.GetCurrentDirectory());
        await services.MigrateGarageFlowAsync(configuration, CreateEnvironment(), Directory.GetCurrentDirectory());

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        Assert.Single(await dbContext.Users.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task MigrateGarageFlowAsync_ShouldNotReadSeedScript_WhenAutoSeedIsFalse()
    {
        var root = new InMemoryDatabaseRoot();
        await using var services = CreateServiceProvider(root);
        var configuration = CreateConfiguration(
            autoMigrate: false,
            autoSeed: false,
            seedScriptPath: "this-file-must-not-exist.sql");

        await services.MigrateGarageFlowAsync(configuration, CreateEnvironment(), Directory.GetCurrentDirectory());
    }

    [Fact]
    public async Task MigrateGarageFlowAsync_ShouldPropagateCancellation()
    {
        var root = new InMemoryDatabaseRoot();
        await using var services = CreateServiceProvider(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => services.MigrateGarageFlowAsync(
            CreateConfiguration(autoMigrate: false),
            CreateEnvironment(),
            Directory.GetCurrentDirectory(),
            cancellation.Token));
    }

    private static ServiceProvider CreateServiceProvider(InMemoryDatabaseRoot root)
    {
        var services = new ServiceCollection();
        var databaseName = $"database-initialization-{Guid.NewGuid():N}";
        services.AddLogging();
        services.AddDbContext<GarageFlowDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, root));
        services.AddScoped<GarageFlow.Application.Auth.Abstractions.IPasswordHashService, PasswordHashService>();
        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(
        bool autoMigrate,
        bool autoSeed = false,
        string? seedScriptPath = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:AutoMigrate"] = autoMigrate.ToString(),
                ["Database:AutoSeed"] = autoSeed.ToString(),
                ["Database:SeedScriptPath"] = seedScriptPath,
                ["Auth:BootstrapAdmin:FullName"] = "Migration Admin",
                ["Auth:BootstrapAdmin:Email"] = "migration.admin@garageflow.test",
                ["Auth:BootstrapAdmin:BirthDate"] = "1990-01-01",
                ["Auth:BootstrapAdmin:Password"] = "MigrationAdmin123!"
            })
            .Build();
    }

    private static IHostEnvironment CreateEnvironment()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns("IntegrationTests");
        return environment.Object;
    }
}
