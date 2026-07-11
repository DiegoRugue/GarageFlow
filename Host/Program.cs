using GarageFlow.Adapters.Api.Auth;
using GarageFlow.Adapters.Api.Customers;
using GarageFlow.Adapters.Api.InventoryItems;
using GarageFlow.Adapters.Api.Security;
using GarageFlow.Adapters.Api.Services;
using GarageFlow.Adapters.Api.Users;
using GarageFlow.Adapters.Api.Vehicles;
using GarageFlow.Adapters.Api.WorkOrders;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Application.Common.Behaviors;
using GarageFlow.Application.Common.Events;
using GarageFlow.Host.Middlewares;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(TransactionBehavior<,>)];
});
builder.Services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();
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

await app.RunAsync();

public partial class Program;
