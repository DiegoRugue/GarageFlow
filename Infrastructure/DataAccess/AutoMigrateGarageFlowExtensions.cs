using System.Globalization;
using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.BuildingBlocks.Domain.ValueObjects;
using GarageFlow.Domain.Users.Entities;
using GarageFlow.Domain.Users.Enums;
using GarageFlow.Infrastructure.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GarageFlow.Infrastructure.DataAccess;

public static partial class AutoMigrateGarageFlowExtensions
{
    public static WebApplication AutoMigrateGarageFlow(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var shouldAutoMigrate = app.Configuration.GetValue<bool?>("Database:AutoMigrate") ?? app.Environment.IsDevelopment();
        if (!shouldAutoMigrate)
        {
            return app;
        }

        using var scope = app.Services.CreateScope();
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

        return app;
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
}
