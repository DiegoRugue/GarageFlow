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
        "Estimate decision webhook HMAC secret must be a non-placeholder value of at least 32 characters outside Development and IntegrationTests.")
    .ValidateOnStart();
builder.Services.AddOpenApi();
builder.AddGarageFlowAuthentication();
builder.Services.AddGarageFlowInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();
app.Services.AutoMigrateGarageFlow(app.Configuration, app.Environment, app.Environment.ContentRootPath);
app.UseGarageFlowExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
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
    if (environment.IsDevelopment()
        || environment.IsEnvironment("Integration")
        || environment.IsEnvironment("IntegrationTests"))
    {
        return true;
    }

    return secret is { Length: >= 32 }
        && !secret.Contains("__", StringComparison.Ordinal)
        && !secret.Contains("SET_ME", StringComparison.OrdinalIgnoreCase)
        && !secret.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
        && !secret.Contains("CONFIGURE", StringComparison.OrdinalIgnoreCase)
        && !secret.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase);
}

public partial class Program;
