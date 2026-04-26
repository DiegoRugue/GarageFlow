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
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GarageFlow.Infrastructure.DataAccess;

public static class AutoMigrateGarageFlowExtensions
{
    public static WebApplication AutoMigrateGarageFlow(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var shouldAutoMigrate = app.Configuration.GetValue<bool?>("Database:AutoMigrate") ?? true;
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
            logger.LogWarning(
                "Skipped bootstrap admin creation because configuration section '{SectionName}' is incomplete.",
                BootstrapAdminOptions.SectionName);
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
                logger.LogInformation("Bootstrap admin creation skipped because another instance already created a user.");
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
}
