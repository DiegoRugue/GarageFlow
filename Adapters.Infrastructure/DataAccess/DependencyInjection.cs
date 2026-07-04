using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.Application.WorkOrders.Abstractions;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Domain.Customers.Repositories;
using GarageFlow.Domain.InventoryItems.Repositories;
using GarageFlow.Domain.Services.Repositories;
using GarageFlow.Domain.Users.Repositories;
using GarageFlow.Domain.Vehicles.Repositories;
using GarageFlow.Domain.WorkOrders.Repositories;
using GarageFlow.Adapters.Infrastructure.Auth;
using GarageFlow.Adapters.Infrastructure.Auth.Jwt;
using GarageFlow.Adapters.Infrastructure.Customers.Repositories;
using GarageFlow.Adapters.Infrastructure.InventoryItems.Repositories;
using GarageFlow.Adapters.Infrastructure.Services.Repositories;
using GarageFlow.Adapters.Infrastructure.Users.Repositories;
using GarageFlow.Adapters.Infrastructure.Vehicles.Repositories;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Email;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GarageFlow.Adapters.Infrastructure.DataAccess;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddGarageFlowDataAccess(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var databaseProvider = ResolveDatabaseProvider(builder.Configuration["Database:Provider"]);
        var useInMemoryProvider =
            databaseProvider == "InMemory" ||
            builder.Environment.IsEnvironment("IntegrationTests");
        if (useInMemoryProvider)
        {
            var databaseName = builder.Configuration["Database:DatabaseName"];
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = "garageflow";
            }

            builder.Services.AddDbContext<GarageFlowDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName);
            });
        }
        else
        {
            var connectionString = builder.Configuration.GetConnectionString("GarageFlow");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Missing required connection string 'ConnectionStrings:GarageFlow' for Postgres provider. " +
                    "Configure it via appsettings or the environment variable 'ConnectionStrings__GarageFlow'.");
            }

            builder.Services.AddDbContext<GarageFlowDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
            });
        }

        builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
        builder.Services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
        builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
        builder.Services.AddScoped<IVehicleBrandRepository, VehicleBrandRepository>();
        builder.Services.AddScoped<IVehicleModelRepository, VehicleModelRepository>();
        builder.Services.AddScoped<IVehicleColorRepository, VehicleColorRepository>();
        builder.Services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
        builder.Services.AddScoped<ICustomerApprovalEmailSender, LoggingCustomerApprovalEmailSender>();
        builder.Services.AddScoped<IPasswordHashService, PasswordHashService>();
        builder.Services.AddScoped<ITokenService, JwtTokenService>();
        builder.Services.Configure<JwtTokenOptions>(builder.Configuration.GetSection(JwtTokenOptions.SectionName));
        builder.Services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<GarageFlowDbContext>());

        return builder;
    }

    private static string ResolveDatabaseProvider(string? configuredProvider)
    {
        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            return "Postgres";
        }

        if (string.Equals(configuredProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return "InMemory";
        }

        if (string.Equals(configuredProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
        {
            return "Postgres";
        }

        throw new InvalidOperationException(
            $"Invalid value for configuration key 'Database:Provider': '{configuredProvider}'. " +
            "Allowed values are 'InMemory', 'Postgres', or empty/null (defaults to 'Postgres').");
    }
}
