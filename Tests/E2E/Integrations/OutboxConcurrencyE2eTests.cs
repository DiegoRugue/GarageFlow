using GarageFlow.Adapters.Infrastructure.DataAccess;
using GarageFlow.Adapters.Infrastructure.Integrations.Outbox;
using GarageFlow.Application.Common.Integrations;
using GarageFlow.Application.WorkOrders.Integrations;
using GarageFlow.Tests.E2E.Support.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GarageFlow.Tests.E2E.Integrations;

[Collection(OutboxLifecycleE2eCollectionDefinition.Name)]
public sealed class OutboxConcurrencyE2eTests(OutboxLifecycleE2eFixture fixture)
{
    private readonly OutboxLifecycleE2eFixture _fixture = fixture;

    [Fact]
    public async Task Worker_ShouldPublishExactEvent_AndMarkCommittedRowProcessed()
    {
        await ResetAsync();
        var expected = CreateEvent();
        var messageId = await InsertAsync(expected, DateTime.UtcNow);

        var actual = await _fixture.Publisher.WaitForPublishedAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(expected, actual);
        await WaitForRowAsync(messageId, row => row.ProcessedAt is not null);

        var row = await GetRowAsync(messageId);
        Assert.Equal(1, row.AttemptCount);
        Assert.Null(row.LeaseId);
        Assert.Null(row.LastError);
    }

    [Fact]
    public async Task Worker_ShouldRescheduleInjectedFailure_ThenEventuallyPublishExactlyOnce()
    {
        await ResetAsync(failures: 1);
        var expected = CreateEvent();
        var messageId = await InsertAsync(expected, DateTime.UtcNow);

        await WaitForRowAsync(messageId, row => row.AttemptCount == 1 && row.LastError != null);
        var retried = await _fixture.Publisher.WaitForPublishedAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(expected, retried);
        await WaitForRowAsync(messageId, row => row.ProcessedAt is not null);

        var row = await GetRowAsync(messageId);
        Assert.Equal(2, row.AttemptCount);
        Assert.Equal(2, _fixture.Publisher.Attempts.Count);
        Assert.Null(row.LastError);
    }

    [Fact]
    public async Task TwoProcessors_ShouldClaimDisjointRows_AndStaleOwnerCannotCompleteOrReschedule()
    {
        await ResetAsync();
        var repositoryNow = DateTime.UtcNow.AddHours(1);
        var ids = new[]
        {
            await InsertAsync(CreateEvent(), repositoryNow),
            await InsertAsync(CreateEvent(), repositoryNow),
            await InsertAsync(CreateEvent(), repositoryNow),
            await InsertAsync(CreateEvent(), repositoryNow)
        };

        using var firstScope = _fixture.CreateScope();
        using var secondScope = _fixture.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstLease = Guid.NewGuid();
        var secondLease = Guid.NewGuid();
        var firstTask = Task.Run(async () =>
        {
            await start.Task;
            return await first.ClaimBatchAsync(firstLease, repositoryNow, repositoryNow.AddMinutes(1), 2);
        });
        var secondTask = Task.Run(async () =>
        {
            await start.Task;
            return await second.ClaimBatchAsync(secondLease, repositoryNow, repositoryNow.AddMinutes(1), 2);
        });
        start.SetResult();
        var batches = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(2, batches[0].Count);
        Assert.Equal(2, batches[1].Count);
        Assert.Empty(batches[0].Select(row => row.Id).Intersect(batches[1].Select(row => row.Id)));
        Assert.True(ids.ToHashSet().SetEquals(batches.SelectMany(batch => batch).Select(row => row.Id)));
        var claim = batches[0][0];
        Assert.False(await second.MarkProcessedAsync(claim.Id, secondLease, repositoryNow));
        Assert.False(await second.RescheduleAsync(claim.Id, secondLease, repositoryNow, "stale"));
        Assert.True(await first.MarkProcessedAsync(claim.Id, firstLease, repositoryNow));
    }

    [Fact]
    public async Task TwoProcessorInstances_ShouldPublishDisjointMessagesThroughRealPostgreSqlClaims()
    {
        await ResetAsync();
        var now = DateTime.UtcNow.AddHours(1);
        var expected = Enumerable.Range(0, 4).Select(_ => CreateEvent()).ToArray();
        foreach (var notification in expected)
        {
            _ = await InsertAsync(notification, now);
        }

        var options = Options.Create(new IntegrationOutboxOptions
        {
            Enabled = true,
            BatchSize = 2,
            PollingIntervalSeconds = 1,
            LeaseDurationSeconds = 30,
            InitialRetryDelaySeconds = 1,
            MaxRetryDelaySeconds = 2
        });
        using var firstScope = _fixture.CreateScope();
        using var secondScope = _fixture.CreateScope();
        var first = new IntegrationOutboxProcessor(
            firstScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>(),
            _fixture.Publisher,
            options,
            new FixedTimeProvider(now),
            NullLogger<IntegrationOutboxProcessor>.Instance);
        var second = new IntegrationOutboxProcessor(
            secondScope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>(),
            _fixture.Publisher,
            options,
            new FixedTimeProvider(now),
            NullLogger<IntegrationOutboxProcessor>.Instance);

        var results = await Task.WhenAll(first.ProcessBatchAsync(), second.ProcessBatchAsync());

        Assert.All(results, result =>
        {
            Assert.Equal(2, result.ClaimedCount);
            Assert.Equal(2, result.ProcessedCount);
        });
        Assert.Equal(4, _fixture.Publisher.Attempts.Count);
        Assert.Equal(
            expected.Select(item => item.WorkOrderId).Order(),
            _fixture.Publisher.Attempts.Select(item => item.WorkOrderId).Order());
        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM \"IntegrationOutboxMessages\" WHERE \"ProcessedAt\" IS NOT NULL",
            connection);
        Assert.Equal(4L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task ExpiredLease_ShouldBeReclaimed_ButProcessedRowNeverReclaimed()
    {
        await ResetAsync();
        var now = DateTime.UtcNow.AddHours(1);
        var reclaimId = await InsertAsync(CreateEvent(), now);
        var processedId = await InsertAsync(CreateEvent(), now);
        using var scope = _fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIntegrationOutboxRepository>();

        var initialLease = Guid.NewGuid();
        var initial = await repository.ClaimBatchAsync(initialLease, now, now.AddSeconds(1), 2);
        Assert.Equal(2, initial.Count);
        Assert.True(await repository.MarkProcessedAsync(processedId, initialLease, now));

        var reclaimLease = Guid.NewGuid();
        var reclaimed = Assert.Single(await repository.ClaimBatchAsync(
            reclaimLease,
            now.AddSeconds(2),
            now.AddMinutes(1),
            2));
        Assert.Equal(reclaimId, reclaimed.Id);
        Assert.Equal(2, reclaimed.AttemptCount);
        Assert.NotEqual(processedId, reclaimed.Id);
    }

    private async Task ResetAsync(int failures = 0)
    {
        _fixture.Publisher.Reset(failures);
        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM \"IntegrationOutboxMessages\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> InsertAsync(WorkOrderStatusChangedIntegrationEvent notification, DateTime nextAttemptAt)
    {
        var id = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(_fixture.DatabaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "IntegrationOutboxMessages"
                ("Id", "EventKey", "AggregateId", "Payload", "OccurredAt", "AttemptCount", "NextAttemptAt")
            VALUES
                (@id, @eventKey, @aggregateId, @payload::jsonb, @occurredAt, 0, @nextAttemptAt)
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("eventKey", WorkOrderStatusChangedIntegrationEvent.EventKey);
        command.Parameters.AddWithValue("aggregateId", notification.WorkOrderId);
        command.Parameters.AddWithValue("payload", IntegrationEventJson.Serialize(notification));
        command.Parameters.AddWithValue("occurredAt", notification.OccurredAt);
        command.Parameters.AddWithValue("nextAttemptAt", nextAttemptAt);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private async Task WaitForRowAsync(Guid id, Func<IntegrationOutboxMessageEntity, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var row = await GetRowAsync(id);
            if (condition(row))
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Outbox row {id} did not reach the expected state.");
    }

    private async Task<IntegrationOutboxMessageEntity> GetRowAsync(Guid id)
    {
        using var scope = _fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<GarageFlowDbContext>()
            .IntegrationOutboxMessages.AsNoTracking().SingleAsync(row => row.Id == id);
    }

    private static WorkOrderStatusChangedIntegrationEvent CreateEvent() => new(
        Guid.NewGuid(),
        "WaitingApproval",
        "InProgress",
        new DateTime(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc));

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
