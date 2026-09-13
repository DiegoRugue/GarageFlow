using Amazon;
using Amazon.SimpleNotificationService;
using GarageFlow.Application.Auth.Abstractions;
using GarageFlow.SharedKernel.Persistence;
using GarageFlow.Application.Customers.Ports;
using GarageFlow.Application.InventoryItems.Ports;
using GarageFlow.Application.Services.Ports;
using GarageFlow.Application.Users.Ports;
using GarageFlow.Application.Vehicles.Ports;
using GarageFlow.Application.WorkOrders.Ports;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Adapters.Infrastructure.Auth;
using GarageFlow.Adapters.Infrastructure.Auth.Jwt;
using GarageFlow.Adapters.Infrastructure.Customers.Repositories;
using GarageFlow.Adapters.Infrastructure.InventoryItems.Repositories;
using GarageFlow.Adapters.Infrastructure.Services.Repositories;
using GarageFlow.Adapters.Infrastructure.Users.Repositories;
using GarageFlow.Adapters.Infrastructure.Vehicles.Repositories;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Repositories;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Idempotency;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Inbox;
using GarageFlow.Adapters.Infrastructure.WorkOrders.Notifications;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GarageFlow.Adapters.Infrastructure.DataAccess;

public static class DependencyInjection
{
    public static IServiceCollection AddGarageFlowInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var databaseProvider = ResolveDatabaseProvider(configuration["Database:Provider"]);
        var useInMemoryProvider =
            databaseProvider == "InMemory" ||
            environment.IsEnvironment("IntegrationTests");
        if (useInMemoryProvider)
        {
            var databaseName = configuration["Database:DatabaseName"];
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = "garageflow";
            }

            services.AddDbContext<GarageFlowDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName);
            });
        }
        else
        {
            var connectionString = configuration.GetConnectionString("GarageFlow");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Missing required connection string 'ConnectionStrings:GarageFlow' for Postgres provider. " +
                    "Configure it via appsettings or the environment variable 'ConnectionStrings__GarageFlow'.");
            }

            services.AddDbContext<GarageFlowDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
            });
        }

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IInventoryItemQueries, EfInventoryItemQueries>();
        services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVehicleQueries, EfVehicleQueries>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IVehicleBrandRepository, VehicleBrandRepository>();
        services.AddScoped<IVehicleModelRepository, VehicleModelRepository>();
        services.AddScoped<IVehicleColorRepository, VehicleColorRepository>();
        services.AddScoped<IWorkOrderQueries, EfWorkOrderQueries>();
        services.AddScoped<IWorkOrderMetricsQueries, EfWorkOrderMetricsQueries>();
        services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
        services.AddScoped<IWorkOrderIntakeRequestStore, WorkOrderIntakeRequestStore>();
        services.AddScoped<IEstimateDecisionInbox, EstimateDecisionInbox>();
        services.AddScoped<IIntegrationOutboxMapper, WorkOrderIntegrationOutboxMapper>();
        services.AddScoped<IOutboxWriter, EfOutboxWriter>();
        services.AddScoped<IIntegrationOutboxRepository, IntegrationOutboxRepository>();
        services.AddSingleton<IValidateOptions<IntegrationOutboxOptions>, IntegrationOutboxOptionsValidator>();
        services.AddOptions<IntegrationOutboxOptions>()
            .Bind(configuration.GetSection(IntegrationOutboxOptions.SectionName))
            .ValidateOnStart();
        if (configuration.GetValue<bool>($"{IntegrationOutboxOptions.SectionName}:Enabled"))
        {
            services.AddSingleton<IValidateOptions<AmazonSnsStatusNotificationOptions>,
                AmazonSnsStatusNotificationOptionsValidator>();
            services.AddOptions<AmazonSnsStatusNotificationOptions>()
                .Bind(configuration.GetSection(AmazonSnsStatusNotificationOptions.SectionName))
                .ValidateOnStart();
            services.AddSingleton<IAmazonSimpleNotificationService>(
                _ => new AmazonSimpleNotificationServiceClient(RegionEndpoint.USEast1));
            services.AddScoped<IWorkOrderStatusNotificationPublisher,
                AmazonSnsWorkOrderStatusNotificationPublisher>();
            services.AddScoped<IIntegrationOutboxProcessor, IntegrationOutboxProcessor>();
            services.AddHostedService<IntegrationOutboxPublisher>();
        }
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.Configure<JwtTokenOptions>(configuration.GetSection(JwtTokenOptions.SectionName));
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<GarageFlowDbContext>());

        return services;
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
