using GarageFlow.Api.Customers;
using GarageFlow.Api.Middlewares;
using GarageFlow.Infrastructure.DataAccess;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});
builder.Services.AddOpenApi();
builder.AddGarageFlowDataAccess();

var app = builder.Build();
app.AutoMigrateGarageFlow();
app.UseGarageFlowExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
if (app.Environment.IsEnvironment("IntegrationTests"))
{
    app.MapGet(
        "/integration-tests/throw/unhandled",
        (HttpContext _) => throw new InvalidOperationException("Integration test exception."));
}
app.MapCustomerEndpoints();

app.Run();

public partial class Program;
