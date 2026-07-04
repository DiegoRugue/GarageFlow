using System.Globalization;
using System.IO;
using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.SharedKernel.Domain.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Adapters.Infrastructure.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GarageFlow.Adapters.Infrastructure.DataAccess;

public static partial class AutoMigrateGarageFlowExtensions
{
    private const string DefaultSeedScriptPath = "scripts/seed-local.sql";

    public static WebApplication AutoMigrateGarageFlow(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var shouldAutoMigrate = app.Configuration.GetValue<bool?>("Database:AutoMigrate") ?? app.Environment.IsDevelopment();
        if (!shouldAutoMigrate)
        {
            return app;
        }

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(AutoMigrateGarageFlowExtensions));
        var dbContext = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();

        if (dbContext.Database.IsRelational())
        {
            dbContext.Database.Migrate();
        }
        else
        {
            dbContext.Database.EnsureCreated();
        }

        EnsureBootstrapAdmin(scope.ServiceProvider, app.Configuration);
        ApplyLocalSeed(app, dbContext, logger);

        return app;
    }

    private static void ApplyLocalSeed(WebApplication app, GarageFlowDbContext dbContext, ILogger logger)
    {
        var shouldAutoSeed = app.Configuration.GetValue<bool?>("Database:AutoSeed") ?? false;
        if (!shouldAutoSeed)
        {
            LogLocalSeedDisabled(logger);
            return;
        }

        if (!dbContext.Database.IsRelational() || !dbContext.Database.IsNpgsql())
        {
            LogLocalSeedSkippedUnsupportedProvider(logger, dbContext.Database.ProviderName ?? "<unknown>");
            return;
        }

        var configuredSeedScriptPath = app.Configuration["Database:SeedScriptPath"];
        var seedScriptPath = string.IsNullOrWhiteSpace(configuredSeedScriptPath)
            ? DefaultSeedScriptPath
            : configuredSeedScriptPath;

        if (!TryResolveSeedScriptPath(app.Environment.ContentRootPath, seedScriptPath, out var resolvedSeedScriptPath))
        {
            LogLocalSeedMissingScript(logger, seedScriptPath, app.Environment.ContentRootPath);
            throw new InvalidOperationException(
                $"Database:AutoSeed is enabled, but the seed script was not found. " +
                $"Set 'Database:SeedScriptPath' to a valid file or disable seeding with 'Database:AutoSeed=false'. " +
                $"Configured path: '{seedScriptPath}'. Content root: '{app.Environment.ContentRootPath}'.");
        }

        dbContext.Database.ExecuteSqlRaw(File.ReadAllText(resolvedSeedScriptPath));
        LogLocalSeedApplied(logger, resolvedSeedScriptPath);
    }

    private static bool TryResolveSeedScriptPath(string contentRootPath, string configuredSeedScriptPath, out string resolvedPath)
    {
        if (Path.IsPathRooted(configuredSeedScriptPath))
        {
            var absolutePath = Path.GetFullPath(configuredSeedScriptPath);
            if (File.Exists(absolutePath))
            {
                resolvedPath = absolutePath;
                return true;
            }

            resolvedPath = string.Empty;
            return false;
        }

        var relativePath = Path.GetFullPath(Path.Combine(contentRootPath, configuredSeedScriptPath));
        if (File.Exists(relativePath))
        {
            resolvedPath = relativePath;
            return true;
        }

        resolvedPath = string.Empty;
        return false;
    }

    private static void EnsureBootstrapAdmin(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var dbContext = serviceProvider.GetRequiredService<GarageFlowDbContext>();

        if (dbContext.Users.AsNoTracking().Any())
        {
            return;
        }

        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(AutoMigrateGarageFlowExtensions));

        var options = configuration.GetSection(BootstrapAdminOptions.SectionName).Get<BootstrapAdminOptions>() ?? new BootstrapAdminOptions();
        if (
            string.IsNullOrWhiteSpace(options.FullName) ||
            string.IsNullOrWhiteSpace(options.Email) ||
            string.IsNullOrWhiteSpace(options.BirthDate) ||
            string.IsNullOrWhiteSpace(options.Password))
        {
            LogBootstrapAdminConfigurationIncomplete(logger, BootstrapAdminOptions.SectionName);
            return;
        }

        if (!DateOnly.TryParseExact(options.BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
        {
            throw new InvalidOperationException(
                $"Configuration value '{BootstrapAdminOptions.SectionName}:BirthDate' is invalid. Use ISO format 'yyyy-MM-dd'.");
        }

        var passwordHashService = serviceProvider.GetRequiredService<IPasswordHashService>();
        var admin = User.Create(
            fullName: FullName.Create(options.FullName),
            email: Email.Create(options.Email),
            birthDate: birthDate,
            role: UserRole.Admin,
            passwordHash: passwordHashService.Hash(options.Password));

        try
        {
            dbContext.Users.Add(admin);
            dbContext.SaveChanges();
        }
        catch (DbUpdateException exception)
        {
            dbContext.ChangeTracker.Clear();

            if (IsUniqueConstraintViolation(exception) || dbContext.Users.AsNoTracking().Any())
            {
                LogBootstrapAdminCreationSkipped(logger);
                return;
            }

            throw;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
        }

        return false;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Skipped bootstrap admin creation because configuration section '{SectionName}' is incomplete.")]
    private static partial void LogBootstrapAdminConfigurationIncomplete(ILogger logger, string sectionName);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Bootstrap admin creation skipped because another instance already created a user.")]
    private static partial void LogBootstrapAdminCreationSkipped(ILogger logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Local seed disabled (Database:AutoSeed is false).")]
    private static partial void LogLocalSeedDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Local seed skipped because provider '{ProviderName}' is not relational PostgreSQL.")]
    private static partial void LogLocalSeedSkippedUnsupportedProvider(ILogger logger, string providerName);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Local seed script not found. Configured path: '{ConfiguredPath}'. Content root: '{ContentRootPath}'.")]
    private static partial void LogLocalSeedMissingScript(ILogger logger, string configuredPath, string contentRootPath);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Local seed applied from '{ScriptPath}'.")]
    private static partial void LogLocalSeedApplied(ILogger logger, string scriptPath);
}
