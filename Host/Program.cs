using GarageFlow.Adapters.Api.Auth;
using GarageFlow.Adapters.Api.Customers;
using GarageFlow.Adapters.Api.InventoryItems;
using GarageFlow.Adapters.Api.Security;
using GarageFlow.Adapters.Api.Services;
using GarageFlow.Adapters.Api.Users;
using GarageFlow.Adapters.Api.Vehicles;
using GarageFlow.Adapters.Api.WorkOrders;
using GarageFlow.Adapters.Api.Webhooks;
using GarageFlow.Adapters.Api.Webhooks.EstimateDecisions;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Application.Common.Behaviors;
using GarageFlow.Application.Common.Events;
using GarageFlow.Application.WorkOrders.Common;
using GarageFlow.Host.Middlewares;
using GarageFlow.Host.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(TransactionBehavior<,>)];
});
builder.Services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();
builder.Services.AddScoped<EstimateDecisionProcessor>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EstimateDecisionWebhookSignatureValidator>();
builder.Services.AddOptions<EstimateDecisionWebhookOptions>()
    .Bind(builder.Configuration.GetSection(EstimateDecisionWebhookOptions.SectionName))
    .Validate(
        options => IsValidWebhookSecret(options.HmacSecret, builder.Environment),
        "Estimate decision webhook HMAC secret must be a non-placeholder value of at least 32 characters outside Development and Integration.")
    .ValidateOnStart();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddCheck<GarageFlowDatabaseHealthCheck>("garageflow-database", tags: ["ready"]);
builder.AddGarageFlowAuthentication();
builder.Services.AddGarageFlowInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();
if (args.Contains("--migrate-only", StringComparer.Ordinal))
{
    await app.Services.MigrateGarageFlowAsync(
        app.Configuration,
        app.Environment,
        app.Environment.ContentRootPath);
    return;
}

await app.Services.AutoMigrateGarageFlowAsync(
    app.Configuration,
    app.Environment,
    app.Environment.ContentRootPath);
app.UseGarageFlowExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
if (app.Environment.IsEnvironment("IntegrationTests"))
{
    app.MapGet(
        "/integration-tests/throw/unhandled",
        (HttpContext _) => throw new InvalidOperationException("Integration test exception."));
}
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapCustomerEndpoints();
app.MapServiceEndpoints();
app.MapVehicleEndpoints();
app.MapInventoryItemEndpoints();
app.MapWorkOrderEndpoints();
app.MapWebhookEndpoints();

await app.RunAsync();

static bool IsValidWebhookSecret(string? secret, IHostEnvironment environment)
{
    if (string.IsNullOrWhiteSpace(secret))
    {
        return false;
    }

    if (environment.IsDevelopment()
        || environment.IsEnvironment("Integration"))
    {
        return true;
    }

    return secret.Length >= 32
        && !secret.Contains("__", StringComparison.Ordinal)
        && !secret.Contains("SET_ME", StringComparison.OrdinalIgnoreCase)
        && !secret.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
        && !secret.Contains("CONFIGURE", StringComparison.OrdinalIgnoreCase)
        && !secret.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase);
}

public partial class Program;
