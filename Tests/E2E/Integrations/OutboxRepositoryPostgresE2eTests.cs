using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Tests.E2E.Support.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GarageFlow.Tests.E2E.Integrations;

[Collection(E2eApiCollection.Name)]
public sealed class OutboxRepositoryPostgresE2eTests(E2eApiFixture fixture)
{
    private readonly E2eApiFixture _fixture = fixture;
    private static readonly DateTime Now = new(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ClaimBatchAsync_ShouldOrderDueRowsAndExcludeUnavailableRows()
    {
        await ResetAsync();
        var dueLater = await InsertAsync(Now.AddMinutes(-1), Now.AddSeconds(-1));
        var dueFirst = await InsertAsync(Now.AddMinutes(-2), Now.AddSeconds(-2));
        _ = await InsertAsync(Now, Now.AddMinutes(1));
        _ = await InsertAsync(Now, Now.AddMinutes(-3), processedAt: Now);
        _ = await InsertAsync(Now, Now.AddMinutes(-3), leaseId: Guid.NewGuid(), leaseExpiresAt: Now.AddMinutes(1));
        var expired = await InsertAsync(Now.AddMinutes(-3), Now.AddMinutes(-3), leaseId: Guid.NewGuid(), leaseExpiresAt: Now.AddTicks(-1));
        var leaseId = Guid.NewGuid();

        using var scope = _fixture.CreateScope();
        var repository = new IntegrationOutboxRepository(scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>());
        var claimed = await repository.ClaimBatchAsync(leaseId, Now, Now.AddMinutes(5), 10);

        Assert.Equal([expired, dueFirst, dueLater], claimed.Select(message => message.Id));
        Assert.All(claimed, message =>
        {
            Assert.Equal(leaseId, message.LeaseId);
            Assert.Equal(1, message.AttemptCount);
        });
    }

    [Fact]
    public async Task ClaimBatchAsync_ShouldBreakDueTiesByOccurredAtThenId()
    {
        await ResetAsync();
        var nextAttemptAt = Now.AddMinutes(-1);
        var latestOccurred = await InsertAsync(Now.AddMinutes(-1), nextAttemptAt);
        var highId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        _ = await InsertAsync(Now.AddMinutes(-2), nextAttemptAt, id: highId);
        _ = await InsertAsync(Now.AddMinutes(-2), nextAttemptAt, id: lowId);

        using var scope = _fixture.CreateScope();
        var repository = new IntegrationOutboxRepository(scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>());
        var claimed = await repository.ClaimBatchAsync(Guid.NewGuid(), Now, Now.AddMinutes(5), 10);

        Assert.Equal([lowId, highId, latestOccurred], claimed.Select(message => message.Id));
    }

    [Fact]
    public async Task ClaimBatchAsync_ShouldSkipAnObjectivelyLockedRow()
    {
        await ResetAsync();
        var lockedId = await InsertAsync(Now.AddMinutes(-2), Now.AddMinutes(-2));
        var availableId = await InsertAsync(Now.AddMinutes(-1), Now.AddMinutes(-1));

        await using var blocker = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await blocker.OpenAsync();
        await using var blockerTransaction = await blocker.BeginTransactionAsync();
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT \"Id\" FROM \"IntegrationOutboxMessages\" WHERE \"Id\" = @id FOR UPDATE",
            blocker,
            blockerTransaction))
        {
            lockCommand.Parameters.AddWithValue("id", lockedId);
            _ = await lockCommand.ExecuteScalarAsync();
        }

        using var firstScope = _fixture.CreateScope();
        var first = new IntegrationOutboxRepository(firstScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>());
        var firstClaims = await first.ClaimBatchAsync(Guid.NewGuid(), Now, Now.AddMinutes(5), 1)
            .WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(availableId, Assert.Single(firstClaims).Id);

        await blockerTransaction.CommitAsync();
        using var secondScope = _fixture.CreateScope();
        var second = new IntegrationOutboxRepository(secondScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>());
        var secondClaims = await second.ClaimBatchAsync(Guid.NewGuid(), Now, Now.AddMinutes(5), 1);
        Assert.Equal(lockedId, Assert.Single(secondClaims).Id);
        Assert.Empty(firstClaims.Select(message => message.Id).Intersect(secondClaims.Select(message => message.Id)));
    }

    [Fact]
    public async Task ClaimBatchAsync_ShouldGiveConcurrentWorkersDisjointClaims()
    {
        await ResetAsync();
        var expectedIds = new HashSet<Guid>();
        for (var offset = 4; offset > 0; offset--)
        {
            expectedIds.Add(await InsertAsync(Now.AddMinutes(-offset), Now.AddMinutes(-offset)));
        }

        using var firstScope = _fixture.CreateScope();
        using var secondScope = _fixture.CreateScope();
        var first = new IntegrationOutboxRepository(firstScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>());
        var second = new IntegrationOutboxRepository(secondScope.ServiceProvider.GetRequiredService<GarageFlowDbContext>());
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstTask = Task.Run(async () =>
        {
            await start.Task;
            return await first.ClaimBatchAsync(Guid.NewGuid(), Now, Now.AddMinutes(5), 2);
        });
        var secondTask = Task.Run(async () =>
        {
            await start.Task;
            return await second.ClaimBatchAsync(Guid.NewGuid(), Now, Now.AddMinutes(5), 2);
        });

        start.SetResult();
        var claims = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(2, claims[0].Count);
        Assert.Equal(2, claims[1].Count);
        Assert.Empty(claims[0].Select(message => message.Id).Intersect(claims[1].Select(message => message.Id)));
        Assert.True(expectedIds.SetEquals(claims.SelectMany(batch => batch).Select(message => message.Id)));
    }

    [Fact]
    public async Task ClaimBatchAsync_ShouldIncrementAttemptExactlyOncePerClaimIncludingReclaim()
    {
        await ResetAsync();
        var id = await InsertAsync(Now, Now.AddSeconds(-1));
        using var scope = _fixture.CreateScope();
        var repository = new IntegrationOutboxRepository(scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>());

        var first = Assert.Single(await repository.ClaimBatchAsync(Guid.NewGuid(), Now, Now.AddMinutes(1), 1));
        var second = Assert.Single(await repository.ClaimBatchAsync(Guid.NewGuid(), Now.AddMinutes(2), Now.AddMinutes(3), 1));

        Assert.Equal(id, first.Id);
        Assert.Equal(id, second.Id);
        Assert.Equal(1, first.AttemptCount);
        Assert.Equal(2, second.AttemptCount);
    }

    [Fact]
    public async Task CompletionUpdates_ShouldRequireCurrentLeaseAndBoundError()
    {
        await ResetAsync();
        var processedId = await InsertAsync(Now, Now.AddSeconds(-1), lastError: "previous failure");
        var retryId = await InsertAsync(Now, Now.AddSeconds(-1));
        var leaseId = Guid.NewGuid();
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>();
        var repository = new IntegrationOutboxRepository(context);
        var claimed = await repository.ClaimBatchAsync(leaseId, Now, Now.AddMinutes(5), 10);
        Assert.Equal(2, claimed.Count);

        Assert.False(await repository.MarkProcessedAsync(processedId, Guid.NewGuid(), Now));
        Assert.True(await repository.MarkProcessedAsync(processedId, leaseId, Now));
        Assert.False(await repository.MarkProcessedAsync(processedId, leaseId, Now));
        Assert.False(await repository.RescheduleAsync(retryId, Guid.NewGuid(), Now.AddSeconds(5), "stale"));
        Assert.True(await repository.RescheduleAsync(retryId, leaseId, Now.AddSeconds(5), new string('e', 2_000)));

        var processed = await context.IntegrationOutboxMessages.AsNoTracking().SingleAsync(message => message.Id == processedId);
        Assert.Equal(Now, processed.ProcessedAt);
        Assert.Null(processed.LeaseId);
        Assert.Null(processed.LeaseExpiresAt);
        Assert.Null(processed.LastError);

        var retry = await context.IntegrationOutboxMessages.AsNoTracking().SingleAsync(message => message.Id == retryId);
        Assert.Null(retry.ProcessedAt);
        Assert.Null(retry.LeaseId);
        Assert.Null(retry.LeaseExpiresAt);
        Assert.Equal(1_024, retry.LastError!.Length);
        Assert.Equal(Now.AddSeconds(5), retry.NextAttemptAt);
    }

    private async Task ResetAsync()
    {
        using var client = _fixture.CreateClient();
        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM \"IntegrationOutboxMessages\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> InsertAsync(
        DateTime occurredAt,
        DateTime nextAttemptAt,
        DateTime? processedAt = null,
        Guid? leaseId = null,
        DateTime? leaseExpiresAt = null,
        string? lastError = null,
        Guid? id = null)
    {
        var messageId = id ?? Guid.NewGuid();
        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "IntegrationOutboxMessages"
                ("Id", "EventKey", "AggregateId", "Payload", "OccurredAt", "AttemptCount", "NextAttemptAt", "ProcessedAt", "LeaseId", "LeaseExpiresAt", "LastError")
            VALUES
                (@id, 'test.v1', @aggregateId, '{}'::jsonb, @occurredAt, 0, @nextAttemptAt, @processedAt, @leaseId, @leaseExpiresAt, @lastError)
            """,
            connection);
        command.Parameters.AddWithValue("id", messageId);
        command.Parameters.AddWithValue("aggregateId", Guid.NewGuid());
        command.Parameters.AddWithValue("occurredAt", occurredAt);
        command.Parameters.AddWithValue("nextAttemptAt", nextAttemptAt);
        command.Parameters.AddWithValue(
            "processedAt",
            processedAt.HasValue ? (object)processedAt.Value : DBNull.Value);
        command.Parameters.AddWithValue(
            "leaseId",
            leaseId.HasValue ? (object)leaseId.Value : DBNull.Value);
        command.Parameters.AddWithValue(
            "leaseExpiresAt",
            leaseExpiresAt.HasValue ? (object)leaseExpiresAt.Value : DBNull.Value);
        command.Parameters.AddWithValue("lastError", (object?)lastError ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
        return messageId;
    }
}
