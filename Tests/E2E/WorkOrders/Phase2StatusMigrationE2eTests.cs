using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Tests.E2E.Support.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace GarageFlow.Tests.E2E.WorkOrders;

public sealed class Phase2StatusMigrationE2eTests(E2eApiFixture fixture) : IClassFixture<E2eApiFixture>
{
    private const string PreviousMigration = "20260502194707_EstimateServiceLineExecution";
    private readonly E2eApiFixture _fixture = fixture;

    [Fact]
    public async Task UpgradeFromPreviousSchema_PreservesStatusesAndAddsFunctionalConstraints()
    {
        await using var database = await DedicatedDatabase.CreateAsync(_fixture.DatabaseConnectionString);
        await MigrateAsync(database.ConnectionString, PreviousMigration);
        var ids = await SeedPreviousSchemaAsync(database.ConnectionString);

        await MigrateAsync(database.ConnectionString);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        Assert.Equal("Received", await ReadStatusAsync(connection, "WorkOrders", ids.CreatedWorkOrderId));
        Assert.Equal("InProgress", await ReadStatusAsync(connection, "WorkOrders", ids.ApprovedWorkOrderId));
        Assert.Equal("Approved", await ReadStatusAsync(connection, "WorkOrderEstimates", ids.EstimateId));

        var requestId = Guid.NewGuid();
        await ExecuteAsync(
            connection,
            """
            INSERT INTO "WorkOrderIntakeRequests" ("RequestId", "PayloadHash", "CreatedAt")
            VALUES (@id, repeat('a', 64), now());
            """,
            new NpgsqlParameter("id", requestId));
        var duplicateRequest = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection,
            """
            INSERT INTO "WorkOrderIntakeRequests" ("RequestId", "PayloadHash", "CreatedAt")
            VALUES (@id, repeat('b', 64), now());
            """,
            new NpgsqlParameter("id", requestId)));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicateRequest.SqlState);

        await AssertCaseInsensitiveDuplicateRejectedAsync(
            connection,
            "INSERT INTO \"VehicleBrands\" (\"Id\", \"Name\", \"CreatedAt\", \"UpdatedAt\") VALUES (@id, 'toyota', now(), now());");
        await AssertCaseInsensitiveDuplicateRejectedAsync(
            connection,
            "INSERT INTO \"VehicleColors\" (\"Id\", \"Name\", \"CreatedAt\", \"UpdatedAt\") VALUES (@id, 'black', now(), now());");
        await AssertCaseInsensitiveDuplicateRejectedAsync(
            connection,
            "INSERT INTO \"VehicleModels\" (\"Id\", \"VehicleBrandId\", \"Name\", \"CreatedAt\", \"UpdatedAt\") VALUES (@id, @brandId, 'corolla', now(), now());",
            new NpgsqlParameter("brandId", ids.BrandId));
    }

    [Fact]
    public async Task UpgradeWithCaseOnlyReferenceDuplicates_FailsWithClearDiagnostic()
    {
        await using var database = await DedicatedDatabase.CreateAsync(_fixture.DatabaseConnectionString);
        await MigrateAsync(database.ConnectionString, PreviousMigration);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await ExecuteAsync(
                connection,
                """
                INSERT INTO "VehicleBrands" ("Id", "Name", "CreatedAt", "UpdatedAt")
                VALUES (@firstId, 'Toyota', now(), now()), (@secondId, 'toyota', now(), now());
                """,
                new NpgsqlParameter("firstId", Guid.NewGuid()),
                new NpgsqlParameter("secondId", Guid.NewGuid()));
        }

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => MigrateAsync(database.ConnectionString));

        Assert.Contains("case-insensitive duplicate", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VehicleBrands", exception.ToString(), StringComparison.Ordinal);
    }

    private static async Task<SeedIds> SeedPreviousSchemaAsync(string connectionString)
    {
        var ids = new SeedIds(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            INSERT INTO "Customers" ("Id", "TaxDocument", "FullName", "Email", "PhoneNumber", "CreatedAt", "UpdatedAt")
            VALUES (@customerId, '52998224725', 'Migration Customer', 'migration@example.com', '11912345678', now(), now());
            INSERT INTO "VehicleBrands" ("Id", "Name", "CreatedAt", "UpdatedAt")
            VALUES (@brandId, 'Toyota', now(), now());
            INSERT INTO "VehicleColors" ("Id", "Name", "CreatedAt", "UpdatedAt")
            VALUES (@colorId, 'Black', now(), now());
            INSERT INTO "VehicleModels" ("Id", "VehicleBrandId", "Name", "CreatedAt", "UpdatedAt")
            VALUES (@modelId, @brandId, 'Corolla', now(), now());
            INSERT INTO "Vehicles" ("Id", "CustomerId", "Year", "VehicleBrandId", "VehicleModelId", "VehicleColorId", "LicensePlate", "CreatedAt", "UpdatedAt")
            VALUES (@vehicleId, @customerId, 2020, @brandId, @modelId, @colorId, 'ABC1D23', now(), now());
            INSERT INTO "WorkOrders" ("Id", "CustomerId", "VehicleId", "Status", "CreatedAt", "UpdatedAt")
            VALUES
                (@createdWorkOrderId, @customerId, @vehicleId, 'Created', now(), now()),
                (@approvedWorkOrderId, @customerId, @vehicleId, 'Approved', now(), now());
            INSERT INTO "WorkOrderEstimates" ("Id", "WorkOrderId", "Status", "CreatedAt", "UpdatedAt")
            VALUES (@estimateId, @createdWorkOrderId, 'Approved', now(), now());
            """,
            new NpgsqlParameter("customerId", ids.CustomerId),
            new NpgsqlParameter("brandId", ids.BrandId),
            new NpgsqlParameter("colorId", ids.ColorId),
            new NpgsqlParameter("modelId", ids.ModelId),
            new NpgsqlParameter("vehicleId", ids.VehicleId),
            new NpgsqlParameter("createdWorkOrderId", ids.CreatedWorkOrderId),
            new NpgsqlParameter("approvedWorkOrderId", ids.ApprovedWorkOrderId),
            new NpgsqlParameter("estimateId", ids.EstimateId));
        return ids;
    }

    private static async Task MigrateAsync(string connectionString, string? targetMigration = null)
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new GarageFlowDbContext(options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(targetMigration);
    }

    private static async Task<string?> ReadStatusAsync(NpgsqlConnection connection, string table, Guid id)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"Status\" FROM \"{table}\" WHERE \"Id\" = @id;";
        command.Parameters.AddWithValue("id", id);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task AssertCaseInsensitiveDuplicateRejectedAsync(
        NpgsqlConnection connection,
        string sql,
        params NpgsqlParameter[] parameters)
    {
        var allParameters = parameters.Append(new NpgsqlParameter("id", Guid.NewGuid())).ToArray();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, sql, allParameters));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
    }

    private static async Task<int> ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        params NpgsqlParameter[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddRange(parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private sealed record SeedIds(
        Guid CustomerId,
        Guid BrandId,
        Guid ColorId,
        Guid ModelId,
        Guid VehicleId,
        Guid CreatedWorkOrderId,
        Guid ApprovedWorkOrderId,
        Guid EstimateId);

    private sealed class DedicatedDatabase(string adminConnectionString, string databaseName, string connectionString)
        : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;

        public static async Task<DedicatedDatabase> CreateAsync(string adminConnectionString)
        {
            var databaseName = $"garageflow_migration_{Guid.NewGuid():N}";
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\";";
            await command.ExecuteNonQueryAsync();

            var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
            return new DedicatedDatabase(adminConnectionString, databaseName, builder.ConnectionString);
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);";
            await command.ExecuteNonQueryAsync();
        }
    }
}
