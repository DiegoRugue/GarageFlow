using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Application.Common.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GarageFlow.Tests.Integration.DataAccess;

public sealed class OutboxPersistenceTests
{
    [Fact]
    public async Task WriteAsync_ShouldTrackMappedFieldsWithoutSaving()
    {
        var databaseName = $"garageflow-outbox-writer-tests-{Guid.NewGuid():N}";
        await using var dbContext = CreateInMemoryDbContext(databaseName);
        var writer = new EfOutboxWriter(dbContext);
        var occurredAt = new DateTime(2026, 7, 11, 11, 0, 0, DateTimeKind.Utc);
        var message = new IntegrationOutboxMessage(
            Guid.NewGuid(),
            "work-order.status-changed.v1",
            Guid.NewGuid(),
            "{\"currentStatus\":\"Diagnosing\"}",
            occurredAt,
            "correlation-1");

        await writer.WriteAsync([message], CancellationToken.None);

        var tracked = Assert.Single(dbContext.ChangeTracker.Entries<IntegrationOutboxMessageEntity>());
        Assert.Equal(EntityState.Added, tracked.State);
        Assert.Equal(message.Id, tracked.Entity.Id);
        Assert.Equal(message.EventKey, tracked.Entity.EventKey);
        Assert.Equal(message.AggregateId, tracked.Entity.AggregateId);
        Assert.Equal(message.Payload, tracked.Entity.Payload);
        Assert.Equal(message.OccurredAt, tracked.Entity.OccurredAt);
        Assert.Equal(message.CorrelationId, tracked.Entity.CorrelationId);
        Assert.Equal(0, tracked.Entity.AttemptCount);
        Assert.Equal(occurredAt, tracked.Entity.NextAttemptAt);
        Assert.Null(tracked.Entity.ProcessedAt);
        Assert.Null(tracked.Entity.LastError);
        Assert.Null(tracked.Entity.LeaseId);
        Assert.Null(tracked.Entity.LeaseExpiresAt);

        await using var verificationContext = CreateInMemoryDbContext(databaseName);
        Assert.Empty(await verificationContext.IntegrationOutboxMessages.ToListAsync());
    }

    [Fact]
    public void Model_ShouldDefineExactOutboxSchemaAndPartialPendingIndex()
    {
        using var dbContext = CreatePostgresModelDbContext();
        var entity = dbContext.Model.FindEntityType(typeof(IntegrationOutboxMessageEntity));

        Assert.NotNull(entity);
        Assert.Equal("IntegrationOutboxMessages", entity.GetTableName());
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.Id), "uuid", nullable: false);
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.EventKey), "character varying(128)", nullable: false, maxLength: 128);
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.AggregateId), "uuid", nullable: false);
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.Payload), "jsonb", nullable: false);
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.OccurredAt), "timestamp with time zone", nullable: false);
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.CorrelationId), "character varying(64)", nullable: true, maxLength: 64);
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.AttemptCount), "integer", nullable: false);
        Assert.Equal(0, entity.FindProperty(nameof(IntegrationOutboxMessageEntity.AttemptCount))!.GetDefaultValue());
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.NextAttemptAt), "timestamp with time zone", nullable: false);
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.ProcessedAt), "timestamp with time zone", nullable: true);
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.LastError), "character varying(1024)", nullable: true, maxLength: 1024);
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.LeaseId), "uuid", nullable: true);
        AssertProperty(entity, nameof(IntegrationOutboxMessageEntity.LeaseExpiresAt), "timestamp with time zone", nullable: true);
        Assert.Equal(nameof(IntegrationOutboxMessageEntity.Id), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        var index = Assert.Single(entity.GetIndexes());
        Assert.Equal(
            [nameof(IntegrationOutboxMessageEntity.NextAttemptAt), nameof(IntegrationOutboxMessageEntity.OccurredAt), nameof(IntegrationOutboxMessageEntity.Id)],
            index.Properties.Select(property => property.Name));
        Assert.Equal("\"ProcessedAt\" IS NULL", index.GetFilter());
    }

    [Fact]
    public void Migrations_ShouldContainDeterministicIntegrationOutboxMigrationAfterInboxMigration()
    {
        using var dbContext = CreatePostgresModelDbContext();
        var migrations = dbContext.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();

        var inboxIndex = migrations.IndexOf("20260711100000_EstimateDecisionInbox");
        var outboxIndex = migrations.IndexOf("20260711110000_IntegrationOutbox");
        Assert.True(inboxIndex >= 0);
        Assert.Equal(inboxIndex + 1, outboxIndex);
    }

    private static void AssertProperty(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity,
        string propertyName,
        string columnType,
        bool nullable,
        int? maxLength = null)
    {
        var property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(columnType, property.GetColumnType());
        Assert.Equal(nullable, property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    private static GarageFlowDbContext CreateInMemoryDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"garageflow-outbox-tests-{Guid.NewGuid():N}")
            .Options;
        return new GarageFlowDbContext(options);
    }

    private static GarageFlowDbContext CreatePostgresModelDbContext()
    {
        var options = new DbContextOptionsBuilder<GarageFlowDbContext>()
            .UseNpgsql("Host=localhost;Database=garageflow-model-tests;Username=test;Password=test")
            .Options;
        return new GarageFlowDbContext(options);
    }
}
