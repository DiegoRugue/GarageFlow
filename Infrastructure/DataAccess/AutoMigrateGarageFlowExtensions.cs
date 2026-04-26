using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return app;
    }
}
