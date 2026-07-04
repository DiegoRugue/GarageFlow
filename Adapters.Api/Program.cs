using GarageFlow.Adapters.Api.Auth;
using GarageFlow.Adapters.Api.Customers;
using GarageFlow.Adapters.Api.InventoryItems;
using GarageFlow.Adapters.Api.Middlewares;
using GarageFlow.Adapters.Api.Security;
using GarageFlow.Adapters.Api.Services;
using GarageFlow.Adapters.Api.Users;
using GarageFlow.Adapters.Api.Vehicles;
using GarageFlow.Adapters.Api.WorkOrders;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});
builder.Services.AddOpenApi();
builder.AddGarageFlowAuthentication();
builder.AddGarageFlowDataAccess();

var app = builder.Build();
app.AutoMigrateGarageFlow();
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
