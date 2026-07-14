using GarageFlow.Adapters.Infrastructure.DataAccess;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GarageFlow.Host.Health;

public sealed class GarageFlowDatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();

        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("GarageFlow database is unavailable.");
    }
}
