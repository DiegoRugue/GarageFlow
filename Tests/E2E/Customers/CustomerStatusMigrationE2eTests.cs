using GarageFlow.Adapters.Infrastructure.Customers.Repositories;
using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Domain.Customers.Enums;
using GarageFlow.Domain.Customers.ValueObjects;
using GarageFlow.Tests.E2E.Support.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace GarageFlow.Tests.E2E.Customers;

[Collection(E2eApiCollection.Name)]
public sealed class CustomerStatusMigrationE2eTests(E2eApiFixture fixture)
{
    private const string PreviousMigration = "20260711110000_IntegrationOutbox";
    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task UpgradeFromPreviousSchema_ShouldBackfillActiveAndRoundTripSuspended()
    {
        await using var database = await DedicatedDatabase.CreateAsync(_fixture.DatabaseConnectionString);
        await MigrateAsync(database.ConnectionString, PreviousMigration);
        var customerId = Guid.NewGuid();
        await InsertLegacyCustomerAsync(database.ConnectionString, customerId);

        await MigrateAsync(database.ConnectionString);

        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using var context = new GarageFlowDbContext(options);
        var repository = new CustomerRepository(context);
        var customer = await repository.GetByIdAsync(CustomerId.From(customerId));
        Assert.NotNull(customer);
        Assert.Equal(CustomerStatus.Active, customer.Status);

        customer.ChangeStatus(CustomerStatus.Suspended);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByTaxDocumentAsync(
            GarageFlow.SharedKernel.Domain.ValueObjects.TaxDocument.Create("529.982.247-25"));
        Assert.NotNull(reloaded);
        Assert.Equal(CustomerStatus.Suspended, reloaded.Status);
        Assert.Equal(EntityState.Detached, context.Entry(reloaded).State);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT \"Status\" FROM \"Customers\" WHERE \"Id\" = @id",
            connection);
        command.Parameters.AddWithValue("id", customerId);
        Assert.Equal("Suspended", await command.ExecuteScalarAsync());

        await using var invalidCommand = new NpgsqlCommand(
            "UPDATE \"Customers\" SET \"Status\" = 'Unknown' WHERE \"Id\" = @id",
            connection);
        invalidCommand.Parameters.AddWithValue("id", customerId);
        var exception = await Assert.ThrowsAsync<PostgresException>(invalidCommand.ExecuteNonQueryAsync);
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    private static async Task InsertLegacyCustomerAsync(string connectionString, Guid customerId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Customers" ("Id", "TaxDocument", "FullName", "Email", "PhoneNumber", "CreatedAt", "UpdatedAt")
            VALUES (@id, '52998224725', 'Legacy Customer', 'legacy.customer@example.com', '11912345678', now(), now())
            """,
            connection);
        command.Parameters.AddWithValue("id", customerId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task MigrateAsync(string connectionString, string? targetMigration = null)
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new GarageFlowDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private sealed class DedicatedDatabase(
        string adminConnectionString,
        string databaseName,
        string connectionString) : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;

        public static async Task<DedicatedDatabase> CreateAsync(string adminConnectionString)
        {
            var databaseName = $"garageflow_customer_status_{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();

            var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
            return new DedicatedDatabase(adminConnectionString, databaseName, builder.ConnectionString);
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",
                connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
